using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OpenFindBearings.Admin.Controllers;

[Authorize]
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
            var health = await client.GetAsync($"{baseUrl}/health/live");
            ViewBag.SyncStatus = health.IsSuccessStatusCode ? "在线" : "离线";
        }
        catch
        {
            ViewBag.SyncStatus = "离线";
        }

        // 预取 ETL 总览与最近任务，注入视图作为首屏数据（JS 后续经代理端点轮询刷新）
        ViewBag.EtlSummaryJson = await TryGetJson(client, $"{baseUrl}/api/etl/summary");
        ViewBag.EtlTasksJson = await TryGetJson(client, $"{baseUrl}/api/etl/tasks?page=1&pageSize=10");
        return View();
    }

    /// <summary>
    /// 尝试拉取 JSON 字符串，失败返回 null（由前端代理轮询补充）
    /// </summary>
    private static async Task<string?> TryGetJson(HttpClient client, string url)
    {
        try
        {
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync();
        }
        catch
        {
            return null;
        }
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
