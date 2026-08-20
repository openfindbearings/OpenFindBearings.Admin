using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models;
using OpenFindBearings.Admin.Services;
using System.Diagnostics;

namespace OpenFindBearings.Admin.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ServiceHealthService _health;
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public HomeController(ServiceHealthService health, IHttpClientFactory factory, IConfiguration config)
    {
        _health = health;
        _factory = factory;
        _config = config;
    }

    public IActionResult Index()
    {
        return View();
    }

    [AllowAnonymous]
    public async Task<IActionResult> Status()
    {
        var result = await _health.CheckAllAsync();
        return Json(result);
    }

    [Authorize]
    public async Task<IActionResult> DataSources()
    {
        var syncBase = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
        var client = _factory.CreateClient("SyncClient");
        try
        {
            var resp = await client.GetAsync($"{syncBase}/api/datasources");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var obj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                return Json(obj.TryGetProperty("data", out var data) ? data : obj);
            }
        }
        catch { }
        return Json(Array.Empty<object>());
    }

    [Authorize]
    public IActionResult Crawler()
    {
        return View("~/Views/Crawler/Index.cshtml");
    }

    [AllowAnonymous]
    public async Task<IActionResult> DashboardStats()
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var syncBase = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
        var apiClient = _factory.CreateClient("ApiClient");
        var syncClient = _factory.CreateClient("SyncClient");
        apiClient.Timeout = TimeSpan.FromSeconds(10);
        syncClient.Timeout = TimeSpan.FromSeconds(5);

        string apiJson;
        try
        {
            var apiResp = await apiClient.GetAsync($"{apiBase}/api/admin/dashboard/stats");
            if (!apiResp.IsSuccessStatusCode)
            {
                return Content(FallbackJson, "application/json");
            }
            apiJson = await apiResp.Content.ReadAsStringAsync();
        }
        catch
        {
            return Content(FallbackJson, "application/json");
        }

        int syncBrandCount = 0, syncTypeCount = 0, syncBearingCount = 0, syncMerchantCount = 0;
        try
        {
            var syncResp = await syncClient.GetAsync($"{syncBase}/api/audit/stats");
            if (syncResp.IsSuccessStatusCode)
            {
                var syncJson = await syncResp.Content.ReadAsStringAsync();
                var syncObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(syncJson);
                if (syncObj.TryGetProperty("data", out var data))
                {
                    syncBrandCount = data.TryGetProperty("brandCount", out var bc) ? bc.GetInt32() : 0;
                    syncTypeCount = data.TryGetProperty("typeCount", out var tc) ? tc.GetInt32() : 0;
                    syncBearingCount = data.TryGetProperty("bearingCount", out var be) ? be.GetInt32() : 0;
                    syncMerchantCount = data.TryGetProperty("merchantCount", out var mc) ? mc.GetInt32() : 0;
                }
            }
        }
        catch { }

        // 在 API JSON 的 data 对象中追加 syncPendingReviews，保持结构精确不变
        using var doc = System.Text.Json.JsonDocument.Parse(apiJson);
        var root = doc.RootElement;
        var syncJsonStr = System.Text.Json.JsonSerializer.Serialize(new
        {
            brandCount = syncBrandCount,
            typeCount = syncTypeCount,
            bearingCount = syncBearingCount,
            merchantCount = syncMerchantCount
        });

        if (root.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var inner = dataElem.GetRawText();
            var merged = inner[..^1] + ",\"syncPendingReviews\":" + syncJsonStr + "}";
            return Content("{\"data\":" + merged + "}", "application/json");
        }

        return Content(apiJson, "application/json");
    }

    private static readonly string FallbackJson = System.Text.Json.JsonSerializer.Serialize(new
    {
        data = new
        {
            bearings = new { totalCount = "N/A", todayAdded = 0, thisWeekAdded = 0, thisMonthAdded = 0, topBrands = Array.Empty<object>(), topTypes = Array.Empty<object>() },
            brands = new { totalCount = "N/A" },
            types = new { totalCount = "N/A" },
            merchants = new { totalCount = "N/A", verifiedCount = 0, pendingApplicationCount = 0, todayRegistered = 0, typeDistribution = Array.Empty<object>() },
            users = new { totalCount = 0, adminCount = 0, merchantStaffCount = 0, individualCount = 0, todayRegistered = 0, activeToday = 0 },
            corrections = new { totalCount = 0, pendingCount = "N/A", approvedCount = 0, rejectedCount = 0, todaySubmitted = 0 },
            pending = new { pendingMerchantBearings = 0, pendingCorrections = 0, pendingLicenses = "N/A" }
        }
    });

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
