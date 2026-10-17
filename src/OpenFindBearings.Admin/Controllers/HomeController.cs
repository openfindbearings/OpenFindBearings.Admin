using OpenFindBearings.Admin.Authorization;
using OpenFindBearings.Admin.Ext;
using OpenFindBearings.Admin.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models;
using OpenFindBearings.Admin.Services;
using System.Diagnostics;

namespace OpenFindBearings.Admin.Controllers;

[Authorize]
[PanelPermission("dashboard.view")]
public class HomeController : Controller
{
    private readonly ServiceHealthService _health;
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly IReadOnlyList<IAdminModule> _modules;

    public HomeController(ServiceHealthService health, IHttpClientFactory factory, IConfiguration config,
        IReadOnlyList<IAdminModule> modules)
    {
        _health = health;
        _factory = factory;
        _config = config;
        _modules = modules;
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

    // 改动说明：数据源页/互联网触发页与 DashboardStats 的外部 ETL 统计聚合已移除，
    //   仪表盘仅呈现本 API 自身统计。
    [AllowAnonymous]
    public async Task<IActionResult> DashboardStats()
    {
        var apiBase = ApiUrlResolver.Api(_config);
        var apiClient = _factory.CreateClient("ApiClient");
        apiClient.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            var apiResp = await apiClient.GetAsync($"{apiBase}/api/admin/dashboard/stats");
            if (!apiResp.IsSuccessStatusCode)
            {
                return Content(FallbackJson, "application/json");
            }
            var apiJson = await apiResp.Content.ReadAsStringAsync();
            // 改动说明（v2.18.0 通用扩展点）：扩展模块可富化仪表盘 JSON（如并入 互联网数据待办），开源无模块=原样
            foreach (var module in _modules)
            {
                var enriched = await module.EnrichDashboardJsonAsync(HttpContext.RequestServices, apiJson);
                if (!string.IsNullOrEmpty(enriched)) apiJson = enriched;
            }
            return Content(apiJson, "application/json");
        }
        catch
        {
            return Content(FallbackJson, "application/json");
        }
    }

    private static readonly string FallbackJson = System.Text.Json.JsonSerializer.Serialize(new
    {
        data = new
        {
            bearings = new { totalCount = "N/A", internetAdded = new { today = 0, week = 0, month = 0 }, manualAdded = new { today = 0, week = 0, month = 0 }, topBrands = Array.Empty<object>(), topTypes = Array.Empty<object>() },
            brands = new { totalCount = "N/A" },
            types = new { totalCount = "N/A" },
            merchants = new { totalCount = "N/A", verifiedCount = 0, onboardedCount = 0, pendingApplicationCount = 0, internetAdded = new { today = 0, week = 0, month = 0 }, onboarded = new { today = 0, week = 0, month = 0 }, typeDistribution = Array.Empty<object>() },
            users = new { totalCount = 0, adminCount = 0, merchantStaffCount = 0, individualCount = 0, todayRegistered = 0, activeToday = 0 },
            corrections = new { totalCount = 0, pendingCount = "N/A", approvedCount = 0, rejectedCount = 0, todaySubmitted = 0 },
            interchanges = new { totalCount = 0, pendingCount = 0, internetAdded = new { today = 0, week = 0, month = 0 }, manualAdded = new { today = 0, week = 0, month = 0 } },
            periods = new { today = "", weekStart = "", monthStart = "" },
            pending = new { pendingMerchantBearings = 0, pendingCorrections = 0, pendingDocuments = "N/A" }
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
