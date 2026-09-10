using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace OpenFindBearings.Admin.Services
{
    /// <summary>
    /// Admin 令牌服务：集中"取用当前用户有效的访问令牌"，在令牌临期/过期时用 refresh_token 静默刷新并回写 cookie。
    /// 背景：Admin 用手写 OIDC RP，访问令牌仅 10 分钟有效、且刷新此前只在 Profile 页触发，
    /// 导致登录后其它页面超过 10 分钟即 401（数据空白）。改由 BearerTokenHandler 统一经本服务取新鲜令牌。
    /// 说明：刷新使用独立裸 HttpClient（不走带 BearerTokenHandler 的 IdentityClient），避免 handler→服务→handler 递归；
    /// device_id 从请求 cookie 读取并随刷新请求上送，配合 Identity 的设备绑定校验。
    /// </summary>
    public class AdminTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminTokenService> _logger;

        // 提前刷新缓冲：距过期不足 60 秒即视为需刷新，规避临界竞态
        private static readonly TimeSpan RenewBuffer = TimeSpan.FromSeconds(60);

        // 改动说明：并发刷新撞滚动 refresh token 的根治。AdminTokenService 是 Scoped（每请求新实例），
        // 实例级锁挡不住并发；且各请求持登录时的旧 refresh 快照，A 刷新触发滚动轮换后旧 refresh 作废，
        // B 再用旧 refresh 必失败→401。故用【进程级、按用户】共享的最新令牌缓存 + 单飞锁：
        // 谁先刷成功就把新令牌写缓存，其余请求拿锁后 double-check 命中缓存即复用、不再消费 refresh。
        // 前提：单副本部署（既定），进程缓存无跨副本一致性问题；管理员数量级个位数，字典不致膨胀。
        private sealed record CachedToken(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CachedToken> _tokenCache = new();

        /// <summary>
        /// 改动说明：登出时清除该用户的进程级令牌缓存。否则登出(其 refresh 已被吊销)后，缓存里旧 refresh
        /// 会在旧 access 过期后触发刷新失败→401 循环。同 userId 重新登录时先清缓存，避免复用登出前的旧令牌。
        /// </summary>
        public static void Invalidate(string? userId)
        {
            if (!string.IsNullOrEmpty(userId)) _tokenCache.TryRemove(userId, out _);
        }

        public AdminTokenService(IConfiguration configuration, ILogger<AdminTokenService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// 取得当前用户有效的访问令牌；临期或强制时刷新并回写 cookie。无令牌或刷新失败返回现有值（可能为空）。
        /// </summary>
        public async Task<string?> GetFreshAccessTokenAsync(HttpContext httpContext, bool forceRefresh = false)
        {
            if (httpContext is null) return null;

            var claims = httpContext.User.Claims.ToList();
            var accessToken = claims.FirstOrDefault(c => c.Type == "access_token")?.Value;
            var refreshToken = claims.FirstOrDefault(c => c.Type == "refresh_token")?.Value;
            var expiresAtRaw = claims.FirstOrDefault(c => c.Type == "expires_at")?.Value;

            if (string.IsNullOrEmpty(accessToken))
            {
                return null;
            }

            var userId = claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? claims.FirstOrDefault(c => c.Type == "sub")?.Value
                         ?? "default";
            // 改动说明：缓存/锁键改用 device_id（每浏览器一条链），而非 userId。
            // 原按 userId 键会让同一账号的多个浏览器共用缓存 → B 拿到 A 的 refresh 却带 B 自己的 device_id
            // → Identity 设备绑定不匹配 → B 刷新失败。按 device_id 键则各浏览器独立，契合设备绑定语义。
            var cacheKey = (!string.IsNullOrEmpty(httpContext.Request.Cookies["device_id"]) ? httpContext.Request.Cookies["device_id"] : null) ?? userId;
            var reqExpiresAt = DateTimeOffset.TryParse(expiresAtRaw, out var ea) ? ea : DateTimeOffset.MinValue;

            // 快路径：非强制且未临期。优先用进程缓存里更新的令牌（可能别的并发请求刚刷过），否则用请求自带令牌。
            if (!forceRefresh)
            {
                if (_tokenCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow + RenewBuffer)
                    return cached.AccessToken;
                if (reqExpiresAt > DateTimeOffset.UtcNow + RenewBuffer)
                    return accessToken;
            }

            if (string.IsNullOrEmpty(refreshToken))
            {
                // 无刷新令牌：只能返回现有（可能已过期），由上层据响应处理
                return accessToken;
            }

            // 慢路径：按 device_id 单飞锁，锁内 double-check 缓存，避免并发重复消费 refresh token。
            var sem = _refreshLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync();
            try
            {
                // 锁内 double-check：可能刚被并发请求刷新完成。
                if (_tokenCache.TryGetValue(cacheKey, out var fresh))
                {
                    if (!forceRefresh && fresh.ExpiresAt > DateTimeOffset.UtcNow + RenewBuffer)
                        return fresh.AccessToken;
                    // 强制刷新(401 触发)时，若缓存令牌比本请求的新（另一请求刚刷成功），直接复用，避免用本请求已作废的 refresh 再刷。
                    if (forceRefresh && fresh.AccessToken != accessToken && fresh.ExpiresAt > reqExpiresAt)
                        return fresh.AccessToken;
                    // 用缓存里最新的 refresh 去刷（而非本请求快照里可能已作废的旧 refresh）。
                    refreshToken = fresh.RefreshToken;
                }

                var refreshed = await RefreshAsync(httpContext, accessToken, refreshToken, claims, cacheKey, forceRefresh);
                return refreshed ?? accessToken;
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// 执行刷新；成功返回新 access_token，写进程缓存并回写 cookie（仅当确有新令牌时）。
        /// </summary>
        private async Task<string?> RefreshAsync(
            HttpContext httpContext,
            string currentAccessToken,
            string refreshToken,
            List<System.Security.Claims.Claim> currentClaims,
            string cacheKey,
            bool forceRefresh)
        {
            // 即便未到临期点，若本次是 401 触发的强制刷新也继续；普通取用且尚未临期则不刷新（上层已拦截）
            var authority = _configuration["Identity:Authority"] ?? "https://localhost:7201";
            var clientId = _configuration["Identity:ClientId"] ?? "admin_client";
            var clientSecret = _configuration["Identity:ClientSecret"] ?? "admin-secret-key";

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                // 改动说明：OpenIddict 拒绝同时携带 Basic 头与表单 client_secret（ID2087 "Multiple client credentials"）。
                // 这里已用 Basic(admin_client:secret) 认证，故表单只留 client_id、不再带 client_secret。
                var form = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
                    ["realm"] = _configuration["Identity:Realm"] ?? "openfindbearings"
                };

                if (httpContext.Request.Cookies.TryGetValue("device_id", out var deviceId) && !string.IsNullOrEmpty(deviceId))
                {
                    form["device_id"] = deviceId;
                }

                var response = await client.PostAsync($"{authority}/connect/token", new FormUrlEncodedContent(form));
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin 令牌刷新失败: {Status} {Body}", response.StatusCode, json);
                    return null;
                }

                var tokenData = System.Text.Json.JsonDocument.Parse(json);
                var newAccessToken = tokenData.RootElement.GetProperty("access_token").GetString();
                var newRefreshToken = tokenData.RootElement.TryGetProperty("refresh_token", out var rt) && rt.ValueKind == System.Text.Json.JsonValueKind.String
                    ? rt.GetString()
                    : refreshToken;
                var expiresIn = tokenData.RootElement.GetProperty("expires_in").GetInt32();

                if (string.IsNullOrEmpty(newAccessToken))
                {
                    return null;
                }

                // 改动说明：先写进程级缓存（按用户），使并发等待方在锁内 double-check 时能命中最新令牌、
                // 不再重复消费 refresh token；再回写 cookie 供后续请求/刷新页面使用。
                _tokenCache[cacheKey] = new CachedToken(newAccessToken, newRefreshToken!, DateTimeOffset.UtcNow.AddSeconds(expiresIn));

                // 回写 cookie（仅浏览器请求上下文可写），使后续请求与页面刷新都用新令牌
                await PersistTokensAsync(httpContext, currentClaims, newAccessToken, newRefreshToken!, expiresIn);
                _logger.LogInformation("Admin 令牌刷新成功，新有效期 {ExpiresIn} 秒（force={Force}）", expiresIn, forceRefresh);
                return newAccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Admin 令牌刷新异常");
                return null;
            }
        }

        /// <summary>
        /// 用刷新得到的新令牌重建 cookie 身份主体并 SignInAsync 续期（保留 name/email/tenant_id/role 等既有声明）。
        /// </summary>
        private async Task PersistTokensAsync(
            HttpContext httpContext,
            List<System.Security.Claims.Claim> currentClaims,
            string accessToken,
            string refreshToken,
            int expiresIn)
        {
            try
            {
                var payload = ParseJwtPayload(accessToken);
                var newClaims = new List<System.Security.Claims.Claim>
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,
                        payload.TryGetValue("sub", out var sub) ? sub : (currentClaims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "")),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name,
                        payload.TryGetValue("name", out var nm) ? nm : (payload.TryGetValue("preferred_username", out var pu) ? pu : (currentClaims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name)?.Value ?? ""))),
                    new System.Security.Claims.Claim("access_token", accessToken),
                    new System.Security.Claims.Claim("refresh_token", refreshToken),
                    new System.Security.Claims.Claim("expires_at", DateTime.UtcNow.AddSeconds(expiresIn).ToString("O"))
                };

                // 保留 email/tenant_id/role 等非令牌类声明（不重复 access/refresh/expires）
                foreach (var c in currentClaims)
                {
                    if (c.Type == "access_token" || c.Type == "refresh_token" || c.Type == "expires_at"
                        || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == System.Security.Claims.ClaimTypes.Name)
                        continue;
                    newClaims.Add(c);
                }

                var identity = new System.Security.Claims.ClaimsIdentity(newClaims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new System.Security.Claims.ClaimsPrincipal(identity);
                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "回写刷新后的令牌 cookie 失败（不影响本次请求使用新令牌）");
            }
        }

        /// <summary>解析 JWT payload（Base64，不验签——仅用于展示/取声明，安全由后端校验兜底）</summary>
        private static Dictionary<string, string> ParseJwtPayload(string token)
        {
            var result = new Dictionary<string, string>();
            try
            {
                var parts = token.Split('.');
                if (parts.Length < 2) return result;
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload += (payload.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                    result[prop.Name] = prop.Value.ToString();
            }
            catch { }
            return result;
        }
    }
}
