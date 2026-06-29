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

    [AllowAnonymous]
    public async Task<IActionResult> DashboardStats()
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var syncBase = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
        var apiClient = _factory.CreateClient("ApiClient");
        var syncClient = _factory.CreateClient("SyncClient");
        apiClient.Timeout = TimeSpan.FromSeconds(10);
        syncClient.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            var apiTask = apiClient.GetAsync($"{apiBase}/api/admin/dashboard/stats");
            var syncTask = syncClient.GetAsync($"{syncBase}/api/audit/stats");
            await Task.WhenAll(apiTask, syncTask);

            var apiResp = await apiTask;
            if (apiResp.IsSuccessStatusCode)
            {
                var apiJson = await apiResp.Content.ReadAsStringAsync();
                var apiObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(apiJson);

                int syncBrandCount = 0, syncTypeCount = 0, syncBearingCount = 0, syncMerchantCount = 0;
                try
                {
                    var syncResp = await syncTask;
                    if (syncResp.IsSuccessStatusCode)
                    {
                        var syncJson = await syncResp.Content.ReadAsStringAsync();
                        var syncObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(syncJson);
                        var data = syncObj.GetProperty("data");
                        syncBrandCount = data.GetProperty("brandCount").GetInt32();
                        syncTypeCount = data.GetProperty("typeCount").GetInt32();
                        syncBearingCount = data.GetProperty("bearingCount").GetInt32();
                        syncMerchantCount = data.GetProperty("merchantCount").GetInt32();
                    }
                }
                catch { }

                var merged = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(apiJson);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(apiJson)
                    ?? new Dictionary<string, object>();

                dict["syncPendingReviews"] = new
                {
                    brandCount = syncBrandCount,
                    typeCount = syncTypeCount,
                    bearingCount = syncBearingCount,
                    merchantCount = syncMerchantCount
                };

                return Json(dict);
            }
        }
        catch { }

        return Json(new { });
    }

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
