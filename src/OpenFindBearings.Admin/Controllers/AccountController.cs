using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Constants;
using OpenFindBearings.Admin.Models.ViewModels;

namespace OpenFindBearings.Admin.Controllers
{
    /// <summary>
    /// 账户控制器 - 处理登录/登出/回调
    /// </summary>
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<AccountController> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// 登录落地页 - 始终显示，未登录用户可见
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl ?? "/";
            return View();
        }

        /// <summary>
        /// 触发 OAuth 登录流程（从落地页点击按钮调用）
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult LoginAction(string? returnUrl = "/")
        {
            var authority = _configuration["Identity:Authority"] ?? "https://localhost:7201";
            var clientId = _configuration["Identity:ClientId"] ?? "admin_client";
            var scope = _configuration["Identity:Scope"] ?? "openid profile email roles api:admin";

            // 生成设备标识并存入 HttpOnly cookie（用于刷新令牌时设备绑定）
            var deviceId = Guid.NewGuid().ToString("N");
            HttpContext.Response.Cookies.Append("device_id", deviceId, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            var authorizationUrl = $"{authority}/connect/authorize" +
                $"?response_type=code" +
                $"&client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(_configuration["Identity:CallbackUrl"] ?? "https://localhost:7167/callback")}" +
                $"&scope={Uri.EscapeDataString(scope)}" +
                $"&state={Guid.NewGuid():N}" +
                $"&realm={TenantConstants.Realm}" +
                $"&device_id={Uri.EscapeDataString(deviceId)}";

            _logger.LogInformation("用户点击登录，跳转 Identity: {Url}", authorizationUrl);
            return Redirect(authorizationUrl);
        }

        /// <summary>
        /// 回调端点 - 用 code 换 token
        /// </summary>
        [HttpGet("~/callback")]
        public async Task<IActionResult> Callback(string code, string state)
        {
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("回调缺少 code 参数");
                return RedirectToAction("Index", "Home");
            }

            var authority = _configuration["Identity:Authority"] ?? "https://localhost:7201";
            var clientId = _configuration["Identity:ClientId"] ?? "admin_client";
            var clientSecret = _configuration["Identity:ClientSecret"] ?? "admin-secret-key";

            try
            {
                var client = _httpClientFactory.CreateClient("IdentityClient");
                var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                var tokenRequestDict = new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = _configuration["Identity:CallbackUrl"] ?? "https://localhost:7167/callback",
                    ["realm"] = TenantConstants.Realm
                };

                // 从 cookie 读取 device_id 并附加到 token 请求
                if (HttpContext.Request.Cookies.TryGetValue("device_id", out var deviceIdFromCookie) &&
                    !string.IsNullOrEmpty(deviceIdFromCookie))
                {
                    tokenRequestDict["device_id"] = deviceIdFromCookie;
                }

                var tokenRequest = new FormUrlEncodedContent(tokenRequestDict);

                _logger.LogInformation("Callback: 开始 token 交换, Authority={Authority}", authority);

                var tokenResponse = await client.PostAsync($"{authority}/connect/token", tokenRequest);
                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

                _logger.LogInformation("Callback: Token 交换响应 {StatusCode}", tokenResponse.StatusCode);

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Token 交换失败: {StatusCode}, {Response}", tokenResponse.StatusCode, tokenJson);
                    return RedirectToAction("Index", "Home");
                }

                // 解析 token
                var tokenData = System.Text.Json.JsonDocument.Parse(tokenJson);
                var accessToken = tokenData.RootElement.GetProperty("access_token").GetString();
                var refreshToken = tokenData.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
                var expiresIn = tokenData.RootElement.GetProperty("expires_in").GetInt32();

                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("Token 交换结果缺少 access_token");
                    return RedirectToAction("Index", "Home");
                }

                // 解析 JWT payload 提取用户信息
                var payload = ParseJwtPayload(accessToken);
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, payload.TryGetValue("sub", out var sub) ? sub : ""),
                    new Claim(ClaimTypes.Name, payload.TryGetValue("name", out var name) ? name : payload.TryGetValue("preferred_username", out var username) ? username : ""),
                    new Claim("access_token", accessToken),
                    new Claim("refresh_token", refreshToken ?? ""),
                    new Claim("expires_at", DateTime.UtcNow.AddSeconds(expiresIn).ToString("O"))
                };
                if (payload.TryGetValue("email", out var email) && !string.IsNullOrEmpty(email))
                    claims.Add(new Claim(ClaimTypes.Email, email));
                if (payload.TryGetValue("tenant_id", out var tenantId) && !string.IsNullOrEmpty(tenantId))
                    claims.Add(new Claim("tenant_id", tenantId));

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    });

                _logger.LogInformation("用户登录成功，Token 有效期 {ExpiresIn} 秒", expiresIn);

                if (state == "changepwd")
                {
                    _logger.LogInformation("状态为 changepwd，跳转至 Identity 修改密码页");
                    var identityAuth = _configuration["Identity:Authority"] ?? "https://localhost:7201";
                    var callbackHost = HttpContext.Request.Host.Value;
                    return Redirect($"{identityAuth}/profile/change-password?returnUrl={Uri.EscapeDataString($"https://{callbackHost}/")}&realm=openfindbearings");
                }

                return Redirect("/");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "回调处理失败");
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// 登出 - 清除本地 Cookie 并重定向到 Identity 结束会话
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 标准 OIDC RP-Initiated Logout：使用 post_logout_redirect_uri 参数
            var authority = _configuration["Identity:Authority"] ?? "https://localhost:7201";
            var host = HttpContext.Request.Host.Value;
            var postLogoutUri = $"https://{host}/signout-callback-oidc";
            _logger.LogInformation("用户已登出，重定向至 Identity 结束会话");
            return Redirect($"{authority}/connect/logout?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutUri)}");
        }

        /// <summary>
        /// OIDC 登出回调端点 - Identity 签出后重定向至此
        /// </summary>
        [HttpGet("~/signout-callback-oidc")]
        public IActionResult SignoutCallbackOidc()
        {
            _logger.LogInformation("OIDC 登出回调，重定向至登录页");
            return RedirectToAction("Login");
        }

        /// <summary>
        /// 个人信息页 - 从 JWT + Identity API + API 项目获取完整信息
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var cookieClaims = HttpContext.User.Claims.ToList();

            var accessToken = cookieClaims.FirstOrDefault(c => c.Type == "access_token")?.Value ?? "";
            var expiresAt = cookieClaims.FirstOrDefault(c => c.Type == "expires_at")?.Value ?? "";

            var model = new OpenFindBearings.Admin.Models.ViewModels.ProfileViewModel
            {
                AccessToken = accessToken.Length > 50 ? accessToken[..50] + "..." : accessToken,
                ExpiresAt = expiresAt
            };

            // 1. 从 JWT payload 提取基本用户信息
            if (!string.IsNullOrEmpty(accessToken))
            {
                var payload = ParseJwtPayload(accessToken);
                model.Id = payload.GetValueOrDefault("sub", "");
                model.UserName = payload.GetValueOrDefault("preferred_username", "");
                model.Email = payload.GetValueOrDefault("email", "");
                model.EmailVerified = payload.GetValueOrDefault("email_verified") == "True";
                model.PhoneNumber = payload.GetValueOrDefault("phone_number", "");
                model.PhoneNumberVerified = payload.GetValueOrDefault("phone_number_verified") == "True";
                model.Name = payload.GetValueOrDefault("name", "");
                model.GivenName = payload.GetValueOrDefault("given_name", "");
                model.FamilyName = payload.GetValueOrDefault("family_name", "");
                model.Nickname = payload.GetValueOrDefault("nickname", "");
                model.Gender = payload.GetValueOrDefault("gender", "");
                model.Birthdate = payload.GetValueOrDefault("birthdate", "");
                model.Locale = payload.GetValueOrDefault("locale", "");
                model.ZoneInfo = payload.GetValueOrDefault("zoneinfo", "");
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                return View(model);
            }

            // 2. 调用 Identity API 获取元数据（创建时间、最后登录等），失败时自动刷新 token 重试
            var identityData = await FetchIdentityProfileAsync(accessToken);
            if (identityData == null && await TryRefreshTokenAsync(cookieClaims))
            {
                // token 已刷新，从更新后的 cookie 重新读取并重试
                cookieClaims = HttpContext.User.Claims.ToList();
                accessToken = cookieClaims.FirstOrDefault(c => c.Type == "access_token")?.Value ?? "";
                model.AccessToken = accessToken.Length > 50 ? accessToken[..50] + "..." : accessToken;
                model.ExpiresAt = cookieClaims.FirstOrDefault(c => c.Type == "expires_at")?.Value ?? "";
                identityData = await FetchIdentityProfileAsync(accessToken);
            }

            if (identityData != null)
            {
                if (identityData.Value.TryGetProperty("lastLoginAt", out var lla) && lla.ValueKind == System.Text.Json.JsonValueKind.String)
                    model.LastLoginAt = lla.GetString();
                if (identityData.Value.TryGetProperty("createdAt", out var ca) && ca.ValueKind == System.Text.Json.JsonValueKind.String)
                    model.CreatedAt = ca.GetString();
                if (identityData.Value.TryGetProperty("updatedAt", out var ua) && ua.ValueKind == System.Text.Json.JsonValueKind.String)
                    model.UpdatedAt = ua.GetString();
                if (identityData.Value.TryGetProperty("isEnabled", out var ie))
                    model.IsEnabled = ie.ValueKind != System.Text.Json.JsonValueKind.False && ie.GetBoolean();
            }
            else
            {
                // 两次均失败（含刷新后重试），已登录用户默认视为启用
                model.IsEnabled = true;
            }

            // 3. 调用 API 项目获取业务角色
            try
            {
                var apiBase = _configuration["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
                var apiClient = _httpClientFactory.CreateClient("ApiClient");
                var apiRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/api/me/roles");
                apiRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var apiResponse = await apiClient.SendAsync(apiRequest);

                if (apiResponse.IsSuccessStatusCode)
                {
                    var json = await apiResponse.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    // ApiResponse<List<string>> 结构：{ success, data: [...] }
                    if (root.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        model.Roles = data.EnumerateArray()
                            .Select(r => r.GetString() ?? "")
                            .Where(r => !string.IsNullOrEmpty(r))
                            .ToList()!;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取 API 项目用户角色失败");
            }

            // 4. JWT 中 role claim 作为兜底
            if (model.Roles.Count == 0)
            {
                var roleClaim = cookieClaims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
                if (roleClaim.Count > 0)
                {
                    model.Roles = roleClaim;
                }
            }

            return View(model);
        }

        /// <summary>
        /// 调用 Identity /api/account/me 获取用户元数据，返回 data 节点或 null
        /// </summary>
        private async Task<System.Text.Json.JsonElement?> FetchIdentityProfileAsync(string accessToken)
        {
            try
            {
                var authority = _configuration["Identity:Authority"] ?? "https://localhost:7201";
                // 使用不带 BearerTokenHandler 的裸 HttpClient，避免重复注入过期 token
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var request = new HttpRequestMessage(HttpMethod.Get, $"{authority}/api/account/me");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Identity /api/account/me 返回 {StatusCode}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var data))
                    return data.Clone();
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "调用 Identity /api/account/me 失败");
                return null;
            }
        }

        /// <summary>
        /// 使用 refresh_token 换取新 access_token，成功后更新 cookie 并返回 true
        /// </summary>
        private async Task<bool> TryRefreshTokenAsync(List<Claim> cookieClaims)
        {
            var refreshToken = cookieClaims.FirstOrDefault(c => c.Type == "refresh_token")?.Value;
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("无 refresh_token，无法刷新");
                return false;
            }

            try
            {
                var authority = _configuration["Identity:Authority"] ?? "https://localhost:7201";
                var clientId = _configuration["Identity:ClientId"] ?? "admin_client";
                var clientSecret = _configuration["Identity:ClientSecret"] ?? "admin-secret-key";

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                var form = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId
                };

                // 附加 device_id（Identity 校验刷新时设备绑定）
                if (HttpContext.Request.Cookies.TryGetValue("device_id", out var deviceId) &&
                    !string.IsNullOrEmpty(deviceId))
                {
                    form["device_id"] = deviceId;
                }

                var response = await client.PostAsync($"{authority}/connect/token", new FormUrlEncodedContent(form));
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Token 刷新失败: {StatusCode}, {Response}", response.StatusCode, json);
                    return false;
                }

                var tokenData = System.Text.Json.JsonDocument.Parse(json);
                var newAccessToken = tokenData.RootElement.GetProperty("access_token").GetString();
                var newRefreshToken = tokenData.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : refreshToken;
                var expiresIn = tokenData.RootElement.GetProperty("expires_in").GetInt32();

                if (string.IsNullOrEmpty(newAccessToken))
                {
                    _logger.LogWarning("Token 刷新结果缺少 access_token");
                    return false;
                }

                // 更新 cookie 中的 token claims
                var newPayload = ParseJwtPayload(newAccessToken);
                var updatedClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, newPayload.TryGetValue("sub", out var newSub) ? newSub : ""),
                    new Claim(ClaimTypes.Name, newPayload.TryGetValue("name", out var newName) ? newName : newPayload.TryGetValue("preferred_username", out var newUsername) ? newUsername : ""),
                    new Claim("access_token", newAccessToken),
                    new Claim("refresh_token", newRefreshToken ?? ""),
                    new Claim("expires_at", DateTime.UtcNow.AddSeconds(expiresIn).ToString("O"))
                };
                if (newPayload.TryGetValue("email", out var newEmail) && !string.IsNullOrEmpty(newEmail))
                    updatedClaims.Add(new Claim(ClaimTypes.Email, newEmail));
                if (newPayload.TryGetValue("tenant_id", out var newTenantId) && !string.IsNullOrEmpty(newTenantId))
                    updatedClaims.Add(new Claim("tenant_id", newTenantId));
                // 保留原有角色 claims
                foreach (var roleClaim in cookieClaims.Where(c => c.Type == ClaimTypes.Role))
                    updatedClaims.Add(roleClaim);

                var identity = new ClaimsIdentity(updatedClaims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    });

                _logger.LogInformation("Token 刷新成功，新有效期 {ExpiresIn} 秒", expiresIn);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token 刷新异常");
                return false;
            }
        }

        /// <summary>
        /// 解析 JWT payload 部分（Base64 解码，不做签名验证）
        /// </summary>
        private static Dictionary<string, string> ParseJwtPayload(string token)
        {
            var result = new Dictionary<string, string>();
            try
            {
                var parts = token.Split('.');
                if (parts.Length < 2) return result;
                var payload = parts[1];
                payload = payload.Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }
                var bytes = Convert.FromBase64String(payload);
                var json = Encoding.UTF8.GetString(bytes);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.ToString();
                }
            }
            catch { }
            return result;
        }
    }
}
