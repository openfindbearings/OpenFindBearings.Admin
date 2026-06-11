# OpenIddict 集成方案 v1.0.0

## 概述

Admin 使用 OpenFindBearings.Identity 项目中的 OpenIddict 作为统一认证中心。Admin 本身不颁发 token，所有认证流程委托给 Identity 服务。

## OpenIddict 配置回顾

Identity 当前配置（来自 `OpenFindBearings.Identity` 项目）：

```csharp
// Program.cs 关键配置
builder.Services.AddOpenIddict()
    .AddCore(options => { ... })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetLogoutEndpointUris("/connect/logout")
               .SetUserinfoEndpointUris("/connect/userinfo");

        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles,
            "api");

        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();

        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableLogoutEndpointPassthrough()
               .EnableUserinfoEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });
```

## Admin 注册为 Confidential Client

在 Identity 启动初始化时注册 Admin 客户端：

```csharp
// Identity 数据库种子数据
if (!await _manager.FindByClientIdAsync("admin_client"))
{
    await _manager.CreateAsync(
        new OpenIddictApplicationDescriptor
        {
            ClientId = "admin_client",
            ClientSecret = "admin-secret-key",
            DisplayName = "OpenFindBearings Admin",
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,

            RedirectUris = { new Uri("https://admin.domain.com/callback") },
            PostLogoutRedirectUris = { new Uri("https://admin.domain.com") },

            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Logout,
                OpenIddictConstants.Permissions.Endpoints.Userinfo,

                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                OpenIddictConstants.Permissions.Scopes.OpenId,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + "api"
            }
        });
}
```

## Admin 侧认证流程

### Startup 配置

```csharp
// Program.cs
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddictValidationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});
```

### Login Action

```csharp
// Controllers/AccountController.cs
public class AccountController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    [HttpGet]
    public IActionResult Login(string returnUrl = "/")
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("Callback"),
            Items = { { "returnUrl", returnUrl } }
        };

        // 构造 OpenIddict 授权请求
        var authorizationUrl = _configuration["Identity:AuthorizationEndpoint"];
        var clientId = _configuration["Identity:ClientId"];
        var redirectUri = Url.Action("Callback", null, null, Request.Scheme);

        var url = $"{authorizationUrl}?" +
                  $"response_type=code" +
                  $"&client_id={clientId}" +
                  $"&redirect_uri={redirectUri}" +
                  $"&scope=openid%20profile%20roles%20api" +
                  $"&state={Guid.NewGuid():N}" +
                  $"&code_challenge=..." +  // PKCE
                  $"&code_challenge_method=S256";

        return Redirect(url);
    }

    [HttpGet]
    public async Task<IActionResult> Callback(string code, string state)
    {
        // 用 authorization code 换取 token
        var tokenResponse = await _httpClient.PostAsync(
            $"{_configuration["Identity:TokenEndpoint"]}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = Url.Action("Callback", null, null, Request.Scheme),
                ["client_id"] = _configuration["Identity:ClientId"],
                ["client_secret"] = _configuration["Identity:ClientSecret"],
                ["code_verifier"] = _codeVerifier  // PKCE
            })
        );

        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

        // 将 token 存入 Cookie
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim("access_token", tokenJson.AccessToken));
        identity.AddClaim(new Claim("refresh_token", tokenJson.RefreshToken));
        identity.AddClaim(new Claim("expires_at",
            DateTime.UtcNow.AddSeconds(tokenJson.ExpiresIn).ToString("O")));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Redirect("/");
    }
}
```

### Token 自动刷新（中间件）

```csharp
// Middleware/TokenRefreshMiddleware.cs
public class TokenRefreshMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        var expiresAt = context.User.FindFirst("expires_at")?.Value;
        if (expiresAt != null &&
            DateTime.Parse(expiresAt) < DateTime.UtcNow.AddMinutes(5))
        {
            // Token 即将过期，刷新
            var refreshToken = context.User.FindFirst("refresh_token")?.Value;
            if (refreshToken != null)
            {
                var newTokens = await RefreshTokensAsync(refreshToken);
                // 更新 Cookie
                await RefreshCookieAsync(context, newTokens);
            }
        }
        await _next(context);
    }
}
```

## API 调用 Token 注入

```csharp
// DelegatingHandler/AuthenticationDelegatingHandler.cs
public class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = _httpContextAccessor.HttpContext?.User
            .FindFirst("access_token")?.Value;

        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

## 日志审计

所有 Admin 的敏感操作（登录/退出/配置修改/用户修改/数据删除）记录审计日志。审计日志由 Admin 自己管理（由于不建 DB，可写入文件日志或推送至 Sync 的审计端点）。

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始设计文档 |
