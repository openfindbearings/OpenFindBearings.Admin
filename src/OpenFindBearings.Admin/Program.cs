using System.Text.Json.Serialization;
using OpenFindBearings.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddHttpClient("ApiClient", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});

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
});

builder.Services.AddScoped<ServiceHealthService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Health check endpoints
app.MapGet("/live", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/ready", () => Results.Ok(new { status = "ready" }));

// Proxy endpoints
app.MapGet("/api/proxy/interchanges/{bearingId:guid}", async (Guid bearingId, IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.GetAsync($"{apiBase}/api/interchanges/by-bearing/{bearingId}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

app.MapGet("/api/proxy/merchant-bearings/{merchantId:guid}", async (Guid merchantId, IHttpClientFactory factory, IConfiguration config) =>
{
    var apiBase = config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
    var client = factory.CreateClient("ApiClient");
    var response = await client.GetAsync($"{apiBase}/api/merchants/{merchantId}/bearings?onlyOnSale=true");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

app.Run();
