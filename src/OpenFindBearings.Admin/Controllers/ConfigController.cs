using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;
using OpenFindBearings.Admin.Services;
using System.Text.Json;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 系统配置管理控制器，代理 API /api/admin/config 端点
/// </summary>
[Authorize]
public class ConfigController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(IHttpClientFactory factory, IConfiguration config, ILogger<ConfigController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 配置列表页，按 Group 分组展示
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.GetAsync($"{apiBase}/api/admin/config");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                // ApiResponse 包装结构：{ success, data: [...] }
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var items = JsonSerializer.Deserialize<List<SystemConfigDto>>(data.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                    // 按 Group 分组排序
                     ViewBag.Items = items;
                     ViewBag.GroupedItems = items
                         .GroupBy(i => i.Group)
                         .OrderBy(g => g.Key)
                         .ToList();
                     return View();
                 }
             }

             // 改动说明：原实现在非 2xx 响应时静默返回空列表，页面显示"暂无配置数据"，
             //           运维无法区分"API 不可用/无权限"与"配置表为空"，难以定位问题
             _logger.LogWarning("获取系统配置返回非成功状态: {StatusCode}", (int)resp.StatusCode);
             TempData["Error"] = $"获取配置失败: HTTP {(int)resp.StatusCode} {resp.StatusCode}";
         }
         catch (Exception ex)
         {
             _logger.LogWarning(ex, "获取系统配置失败");
             TempData["Error"] = $"获取配置失败: {ex.Message}";
         }
         return View();
    }

    /// <summary>
    /// 更新配置值
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string key, string value)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PutAsJsonAsync($"{apiBase}/api/admin/config/{key}", new { value });
            if (resp.IsSuccessStatusCode)
            {
                // 价格配置变更后失效本地缓存，使新值 5 分钟内生效（不必等 TTL）
                if (key.StartsWith("Price.", StringComparison.OrdinalIgnoreCase))
                    PriceConfigService.Invalidate();
                TempData["Success"] = "配置已更新";
            }
            else
            {
                TempData["Error"] = $"更新失败: {resp.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新配置失败: {Key}", key);
            TempData["Error"] = $"更新失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}
