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

    // 改动说明：数据源页/爬虫触发页与 DashboardStats 的外部 ETL 统计聚合已移除，
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
            // 改动说明（v2.18.0 通用扩展点）：闭源模块可富化仪表盘 JSON（如并入 Sync 待办），开源无模块=原样
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
            bearings = new { totalCount = "N/A", todayAdded = 0, thisWeekAdded = 0, thisMonthAdded = 0, topBrands = Array.Empty<object>(), topTypes = Array.Empty<object>() },
            brands = new { totalCount = "N/A" },
            types = new { totalCount = "N/A" },
            merchants = new { totalCount = "N/A", verifiedCount = 0, pendingApplicationCount = 0, todayRegistered = 0, typeDistribution = Array.Empty<object>() },
            users = new { totalCount = 0, adminCount = 0, merchantStaffCount = 0, individualCount = 0, todayRegistered = 0, activeToday = 0 },
            corrections = new { totalCount = 0, pendingCount = "N/A", approvedCount = 0, rejectedCount = 0, todaySubmitted = 0 },
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
