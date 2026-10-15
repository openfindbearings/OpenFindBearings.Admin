using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenFindBearings.Admin.Authorization;
using OpenFindBearings.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC + JSON 配置：camelCase 命名，null 值不序列化
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 改动说明（v2.13.2 僵尸清理）：删除 Admin 自有 ApplicationDbContext/Npgsql 连接——
//   其两张表（AdminAuditLogs/AdminConfigs）全仓从不读写，且违反"Admin 只经 API 代理、不直连库"原则

// 认证配置：Cookie + JWT Bearer
builder.Services.AddAuthorization();
// 改动说明（v1.25.0）：面板权限体系接线——动态策略提供器（Panel:xxx 按需生成）+ claim 处理器；
//   权限数据源=登录时从 API RBAC 拉取写入 cookie 的 permission 多值 claim
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PanelPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PanelPermissionHandler>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        // 改动说明（v1.25.0）：权限 claim 30 分钟复核（ASP.NET SecurityStamp 同款模式）——
        //   登录时写入的 permission/panel_role claim 到期后重拉 API /api/me/permissions：
        //   面板角色被收回即 RejectPrincipal 踢出；否则替换 claim 续期，防权限变更残留。
        //   API 不可达时放行（避免 API 抖动导致后台集体掉线），下轮复核重试
        options.Events.OnValidatePrincipal = async context =>
        {
            if (!context.Properties.IssuedUtc.HasValue
                || DateTime.UtcNow - context.Properties.IssuedUtc.Value.UtcDateTime < TimeSpan.FromMinutes(30))
                return;

            var tokenService = context.HttpContext.RequestServices.GetRequiredService<AdminTokenService>();
            var token = await tokenService.GetFreshAccessTokenAsync(context.HttpContext, false);
            if (string.IsNullOrEmpty(token))
            {
                context.RejectPrincipal();
                return;
            }

            var cfg = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiBase = cfg["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/api/me/permissions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await context.HttpContext.RequestServices
                .GetRequiredService<IHttpClientFactory>().CreateClient().SendAsync(req);
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var roles = new List<string>();
            var perms = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                if (dataEl.TryGetProperty("roles", out var rEl) && rEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                    roles = rEl.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();
                if (dataEl.TryGetProperty("permissions", out var pEl) && pEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                    perms = pEl.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();
            }

            // 改动说明（v2.13.2 收敛）：面板角色集合统一取 PanelRoles.All
            var panelRoles = PanelRoles.All;
            if (!roles.Any(x => panelRoles.Contains(x)))
            {
                context.RejectPrincipal();
                return;
            }

            // 重建 identity：基础 claim（sub/name/token）保留，权限类 claim 与复核时间戳整体替换
            var oldIdentity = context.Principal!.Identity as ClaimsIdentity;
            var newClaims = oldIdentity!.Claims
                .Where(c => c.Type is not ("permission" or "panel_role" or "permissions_fetched_at"))
                .ToList();
            newClaims.AddRange(roles.Select(r => new Claim("panel_role", r)));
            newClaims.AddRange(perms.Select(p => new Claim("permission", p)));
            newClaims.Add(new Claim("permissions_fetched_at", DateTime.UtcNow.ToString("O")));
            context.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(newClaims, CookieAuthenticationDefaults.AuthenticationScheme)));
            context.ShouldRenew = true;
        };
    })
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Identity:Authority"];
        options.Audience = builder.Configuration["Identity:ClientId"];
        options.RequireHttpsMetadata = false;

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception, "JWT 认证失败");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogDebug("JWT 验证成功");
                return Task.CompletedTask;
            }
        };
    });

// 认证方案已配置，默认不强制要求认证
// 各 Controller 按需添加 [Authorize] 或 [AllowAnonymous]
builder.Services.AddHttpContextAccessor();

// Admin 令牌服务：按 exp 预刷新并回写 cookie，供 BearerTokenHandler 取用新鲜访问令牌
builder.Services.AddScoped<AdminTokenService>();
builder.Services.AddScoped<BearerTokenHandler>();

builder.Services.AddHealthChecks();

builder.Services.AddHttpClient("ApiClient", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
#if DEBUG
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
#endif
}).AddHttpMessageHandler<BearerTokenHandler>();


builder.Services.AddHttpClient("IdentityClient", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
#if DEBUG
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
#endif
}).AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddScoped<ServiceHealthService>();
builder.Services.AddSingleton<PriceConfigService>();

var app = builder.Build();

// 改动说明（v2.13.2 僵尸清理）：删除启动 EnsureCreated（随 ApplicationDbContext 一并移除）

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// MVC 路由：默认 Home/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 健康检查端点（不需要认证）
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            }),
            duration = report.TotalDuration
        };
        await context.Response.WriteAsJsonAsync(result);
    }
}).AllowAnonymous();

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        var statusCode = report.Status == HealthStatus.Unhealthy ? 503 : 200;
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(report.Status.ToString());
    }
}).AllowAnonymous();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true
}).AllowAnonymous();

// API 代理端点：前端通过 Admin 中转调用 API，避免跨域问题
// 代理轴承替代品查询（v2.16.0 修正：目标端点为公域 /api/bearings/{id}/interchanges，
//   旧路径 /api/interchanges/by-bearing/{id} 已废弃，修正以保证轴承列表"替代品"弹窗可用）
app.MapGet("/api/proxy/interchanges/{bearingId:guid}", async (Guid bearingId, IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.GetAsync($"{apiBase}/api/bearings/{bearingId}/interchanges");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
}).RequireAuthorization();

// 代理商家商品查询（支持 onlyOnSale 和 dataSource 过滤）
app.MapGet("/api/proxy/merchant-bearings/{merchantId:guid}", async (Guid merchantId, IHttpClientFactory factory, IConfiguration config,
    [FromQuery] bool? onlyOnSale = null, [FromQuery] string? dataSource = null,
    [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var url = $"{apiBase}/api/merchants/{merchantId}/bearings?page={page}&pageSize={pageSize}";
    if (onlyOnSale.HasValue) url += $"&onlyOnSale={onlyOnSale.Value.ToString().ToLower()}";
    if (!string.IsNullOrEmpty(dataSource)) url += $"&dataSource={dataSource}";
    var response = await client.GetAsync(url);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
}).RequireAuthorization();

// 代理轴承在售商家查询（反向：按轴承查商家）
app.MapGet("/api/proxy/bearing-merchants/{bearingId:guid}", async (Guid bearingId, IHttpClientFactory factory, IConfiguration config,
    [FromQuery] bool? onlyOnSale = true, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var url = $"{apiBase}/api/bearings/{bearingId}/merchants?page={page}&pageSize={pageSize}";
    if (onlyOnSale.HasValue) url += $"&onlyOnSale={onlyOnSale.Value.ToString().ToLower()}";
    var response = await client.GetAsync(url);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
}).RequireAuthorization();

// 代理商品审核队列（v2.15.0 商家上架商品人工审核：待审列表 + 通过/驳回）。
// 与上方 /api/proxy/merchant-bearings/{merchantId:guid}（商家在售商品）不同：
// 本组针对"待平台审核的商品"，pending 为字面量段，guid 约束保证两条路由互不冲突
app.MapGet("/api/proxy/merchant-bearings/pending", async (IHttpClientFactory factory, IConfiguration config,
    [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.GetAsync($"{apiBase}/api/admin/merchant-bearings/pending?page={page}&pageSize={pageSize}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
}).RequireAuthorization();

// 代理商品审核通过（通过即在售、公域可见）
app.MapPost("/api/proxy/merchant-bearings/{id:guid}/approve", async (Guid id, IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.PostAsync($"{apiBase}/api/admin/merchant-bearings/{id}/approve", null);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理商品审核驳回（原样透传驳回原因 JSON，商家可见并可修改重提）
app.MapPost("/api/proxy/merchant-bearings/{id:guid}/reject", async (Guid id, System.Text.Json.JsonElement body,
    IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    using var content = new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json");
    var response = await client.PostAsync($"{apiBase}/api/admin/merchant-bearings/{id}/reject", content);
    var resp = await response.Content.ReadAsStringAsync();
    return Results.Content(resp, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// ============ 替代关系治理代理（v2.16.0：平台建标准、审关系） ============
// 前缀 /api/proxy/admin-interchanges 与旧的 /api/proxy/interchanges/{bearingId:guid} 互不冲突

// 代理替代关系管理列表（关键字/审核状态过滤，分页）
app.MapGet("/api/proxy/admin-interchanges", async (IHttpClientFactory factory, IConfiguration config,
    [FromQuery] string? keyword = null, [FromQuery] int? reviewStatus = null,
    [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var url = $"{apiBase}/api/admin/interchanges?page={page}&pageSize={pageSize}";
    if (!string.IsNullOrWhiteSpace(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
    if (reviewStatus.HasValue) url += $"&reviewStatus={reviewStatus.Value}";
    var response = await client.GetAsync(url);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理待审核替代候选队列
app.MapGet("/api/proxy/admin-interchanges/pending", async (IHttpClientFactory factory, IConfiguration config,
    [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.GetAsync($"{apiBase}/api/admin/interchanges/pending?page={page}&pageSize={pageSize}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理平台新建替代关系（原样透传请求体）
app.MapPost("/api/proxy/admin-interchanges", async (System.Text.Json.JsonElement body,
    IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    using var content = new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json");
    var response = await client.PostAsync($"{apiBase}/api/admin/interchanges", content);
    var resp = await response.Content.ReadAsStringAsync();
    return Results.Content(resp, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理平台编辑替代关系（类型/可信度/来源/备注/双向）
app.MapPut("/api/proxy/admin-interchanges/{id:guid}", async (Guid id, System.Text.Json.JsonElement body,
    IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    using var content = new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json");
    var response = await client.PutAsync($"{apiBase}/api/admin/interchanges/{id}", content);
    var resp = await response.Content.ReadAsStringAsync();
    return Results.Content(resp, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理平台删除替代关系（物理删除）
app.MapDelete("/api/proxy/admin-interchanges/{id:guid}", async (Guid id,
    IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.DeleteAsync($"{apiBase}/api/admin/interchanges/{id}");
    var resp = await response.Content.ReadAsStringAsync();
    return Results.Content(resp, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理审核通过替代候选
app.MapPost("/api/proxy/admin-interchanges/{id:guid}/approve", async (Guid id,
    IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.PostAsync($"{apiBase}/api/admin/interchanges/{id}/approve", null);
    var resp = await response.Content.ReadAsStringAsync();
    return Results.Content(resp, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理审核驳回替代候选（原样透传驳回原因 JSON，提交方可见）
app.MapPost("/api/proxy/admin-interchanges/{id:guid}/reject", async (Guid id, System.Text.Json.JsonElement body,
    IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    using var content = new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json");
    var response = await client.PostAsync($"{apiBase}/api/admin/interchanges/{id}/reject", content);
    var resp = await response.Content.ReadAsStringAsync();
    return Results.Content(resp, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理轴承搜索（新建替代关系时选取源/目标轴承；返回分页 items：id/partNumber/brandName/bearingType 等）
app.MapGet("/api/proxy/bearing-search", async (IHttpClientFactory factory, IConfiguration config,
    [FromQuery] string? keyword = null, [FromQuery] int pageSize = 20) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var url = $"{apiBase}/api/bearings/search?page=1&pageSize={pageSize}";
    if (!string.IsNullOrWhiteSpace(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
    var response = await client.GetAsync(url);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理上传/替换勋章图（v2.6.0 勋章图片管线：Admin 传图 → API 存对象存储并回写 ImageKey）
app.MapPost("/api/proxy/achievements/{id:guid}/image", async (Guid id, IFormFile file, IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    using var form = new MultipartFormDataContent();
    using var stream = file.OpenReadStream();
    using var fileContent = new StreamContent(stream);
    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
    form.Add(fileContent, "file", file.FileName);
    var response = await client.PostAsync($"{apiBase}/api/admin/achievements/{id}/image", form);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

app.Run();
