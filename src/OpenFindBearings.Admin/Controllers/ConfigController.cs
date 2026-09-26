using OpenFindBearings.Admin.Authorization;
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
[PanelPermission("system.view")]
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
    /// 配置列表页，按 Group 分组以 tab 页签展示（v1.28.0：末位 tab 为积分赚分规则，
    /// 原独立"积分任务"页并入；规则拉取失败不阻塞配置 tab，单独提示）
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        await LoadPointRulesAsync(client, apiBase);
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
    /// 拉取积分赚分规则供"积分任务"tab 渲染（v1.28.0 自 PointsController.Index 迁入）
    /// </summary>
    private async Task LoadPointRulesAsync(HttpClient client, string apiBase)
    {
        try
        {
            var resp = await client.GetAsync($"{apiBase}/api/admin/points/rules");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    ViewBag.PointRules = JsonSerializer.Deserialize<List<PointRuleDto>>(data.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    return;
                }
            }
            _logger.LogWarning("获取积分规则返回非成功状态: {StatusCode}", (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            // 规则拉取失败仅影响积分 tab，不阻塞配置管理主功能
            _logger.LogWarning(ex, "获取积分规则失败");
        }
        ViewBag.PointRules = new List<PointRuleDto>();
    }

    /// <summary>
    /// 更新配置值（v1.28.0：接收 tab 参数，回跳后停留在原页签）
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [PanelPermission("system.manage")]
    public async Task<IActionResult> Update(string key, string value, string? tab)
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
        // v1.28.0：回跳保留当前页签
        return RedirectToAction("Index", new { tab });
    }
}
