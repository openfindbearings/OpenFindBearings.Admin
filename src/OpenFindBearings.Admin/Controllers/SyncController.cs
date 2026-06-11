using Microsoft.AspNetCore.Mvc;

namespace OpenFindBearings.Admin.Controllers;

public class SyncController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public SyncController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<IActionResult> Index()
    {
        var baseUrl = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
        var client = _factory.CreateClient("SyncClient");
        try
        {
            var health = await client.GetAsync($"{baseUrl}/live");
            ViewBag.SyncStatus = health.IsSuccessStatusCode ? "在线" : "离线";
        }
        catch
        {
            ViewBag.SyncStatus = "离线";
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Trigger(string phase)
    {
        var baseUrl = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
        var client = _factory.CreateClient("SyncClient");
        try
        {
            var resp = await client.PostAsync($"{baseUrl}/api/etl/{phase}", null);
            if (resp.IsSuccessStatusCode)
                TempData["Success"] = $"ETL {phase} 已触发";
            else
                TempData["Error"] = $"触发失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"触发失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}
