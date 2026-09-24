using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenFindBearings.Admin.Authorization;
using OpenFindBearings.Admin.Data;
using OpenFindBearings.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC + JSON 配置：camelCase 命名，null 值不序列化
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Admin 自有数据库：db_admin（PostgreSQL）
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

            var panelRoles = new[] { "Admin", "Operator", "Auditor" };
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

builder.Services.AddHttpClient("SyncClient", c =>
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

// 启动时确保数据库存在（v1.25.0 起本地 RBAC 种子废弃，权限唯一事实源=API）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

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
// 代理轴承替代品查询
app.MapGet("/api/proxy/interchanges/{bearingId:guid}", async (Guid bearingId, IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.GetAsync($"{apiBase}/api/interchanges/by-bearing/{bearingId}");
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

// 代理 Excel 批量导入在售轴承（转发到 Sync API）
app.MapPost("/api/proxy/excel/import-bearing", async (IFormFile file, IHttpClientFactory factory, IConfiguration config) =>
{
    var syncBase = config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
    var client = factory.CreateClient("SyncClient");
    using var form = new MultipartFormDataContent();
    using var stream = file.OpenReadStream();
    using var fileContent = new StreamContent(stream);
    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
    form.Add(fileContent, "file", file.FileName);
    var response = await client.PostAsync($"{syncBase}/api/sync/excel/bearing", form);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理下载 Excel 导入模板
app.MapGet("/api/proxy/excel/template", async (IHttpClientFactory factory, IConfiguration config) =>
{
    var syncBase = config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
    var client = factory.CreateClient("SyncClient");
    var response = await client.GetAsync($"{syncBase}/api/sync/excel/template");
    var bytes = await response.Content.ReadAsByteArrayAsync();
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "bearings_import_template.xlsx");
}).RequireAuthorization();

// 代理 ETL 任务管理总览（各阶段运行态 + 最近任务 + 爬虫最近执行情况）
app.MapGet("/api/proxy/etl/summary", async (IHttpClientFactory factory, IConfiguration config) =>
{
    var syncBase = config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
    var client = factory.CreateClient("SyncClient");
    var response = await client.GetAsync($"{syncBase}/api/etl/summary");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

// 代理 ETL 任务历史列表（支持分页与过滤）
app.MapGet("/api/proxy/etl/tasks", async (IHttpClientFactory factory, IConfiguration config,
    [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
    [FromQuery] string? commandType = null, [FromQuery] string? status = null) =>
{
    var syncBase = config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
    var client = factory.CreateClient("SyncClient");
    var url = $"{syncBase}/api/etl/tasks?page={page}&pageSize={pageSize}";
    if (!string.IsNullOrEmpty(commandType)) url += $"&commandType={commandType}";
    if (!string.IsNullOrEmpty(status)) url += $"&status={status}";
    var response = await client.GetAsync(url);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}).RequireAuthorization();

app.Run();
