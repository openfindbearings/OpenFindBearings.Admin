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

            var authorizationUrl = $"{authority}/connect/authorize" +
                $"?response_type=code" +
                $"&client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(_configuration["Identity:CallbackUrl"] ?? "https://localhost:7167/callback")}" +
                $"&scope={Uri.EscapeDataString(scope)}" +
                $"&state={Guid.NewGuid():N}" +
                $"&realm={TenantConstants.Realm}";

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

                var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = _configuration["Identity:CallbackUrl"] ?? "https://localhost:7167/callback",
                    ["realm"] = TenantConstants.Realm
                });

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
                    return Redirect($"{identityAuth}/profile/change-password?returnUrl={Uri.EscapeDataString("https://localhost:7167/")}&realm=openfindbearings");
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
        /// 登出 - 仅清除本地 Cookie，不影响 Identity 自管理会话
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            _logger.LogInformation("用户已登出，仅清除本地 Cookie");
            return Redirect("/");
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

            // 2. 调用 Identity API 获取元数据（创建时间、最后登录等）
            try
            {
                var authority = _configuration["Identity:Authority"] ?? "https://localhost:7201";
                var identityClient = _httpClientFactory.CreateClient("IdentityClient");
                var identityRequest = new HttpRequestMessage(HttpMethod.Get, $"{authority}/api/account/me");
                identityRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var identityResponse = await identityClient.SendAsync(identityRequest);

                if (identityResponse.IsSuccessStatusCode)
                {
                    var json = await identityResponse.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("data", out var data))
                    {
                        if (data.TryGetProperty("lastLoginAt", out var lla) && lla.ValueKind == System.Text.Json.JsonValueKind.String)
                            model.LastLoginAt = lla.GetString();
                        if (data.TryGetProperty("createdAt", out var ca) && ca.ValueKind == System.Text.Json.JsonValueKind.String)
                            model.CreatedAt = ca.GetString();
                        if (data.TryGetProperty("updatedAt", out var ua) && ua.ValueKind == System.Text.Json.JsonValueKind.String)
                            model.UpdatedAt = ua.GetString();
                        if (data.TryGetProperty("isEnabled", out var ie))
                            model.IsEnabled = ie.ValueKind == System.Text.Json.JsonValueKind.Undefined || ie.GetBoolean();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取 Identity 用户元数据失败");
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
