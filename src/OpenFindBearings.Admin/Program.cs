using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
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

builder.Services.AddTransient<BearerTokenHandler>();

builder.Services.AddHttpClient("ApiClient", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
}).AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddHttpClient("CrawlerClient", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("SyncClient", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("IdentityClient", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
}).AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddScoped<ServiceHealthService>();

var app = builder.Build();

// 启动时确保数据库存在并初始化种子数据（独立 scope 避免 Npgsql 连接状态冲突）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    SeedData.Initialize(db);
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
app.MapGet("/live", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapGet("/ready", () => Results.Ok(new { status = "ready" })).AllowAnonymous();

// API 代理端点：前端通过 Admin 中转调用 API，避免跨域问题
// 代理轴承替代品查询
app.MapGet("/api/proxy/interchanges/{bearingId:guid}", async (Guid bearingId, IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.GetAsync($"{apiBase}/api/interchanges/by-bearing/{bearingId}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// 代理商家在售商品查询
app.MapGet("/api/proxy/merchant-bearings/{merchantId:guid}", async (Guid merchantId, IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.GetAsync($"{apiBase}/api/merchants/{merchantId}/bearings?onlyOnSale=true");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

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
});

// 代理下载 Excel 导入模板
app.MapGet("/api/proxy/excel/template", async (IHttpClientFactory factory, IConfiguration config) =>
{
    var syncBase = config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
    var client = factory.CreateClient("SyncClient");
    var response = await client.GetAsync($"{syncBase}/api/sync/excel/template");
    var bytes = await response.Content.ReadAsByteArrayAsync();
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "bearings_import_template.xlsx");
});

app.Run();
