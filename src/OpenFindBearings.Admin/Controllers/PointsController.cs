using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;
using System.Text.Json;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 积分任务管理控制器（v1.26.0），代理 API /api/admin/points/rules 端点。
/// 赚分规则（分值/每日上限/阶梯/启停）实时生效不发版——运营调分的唯一入口
/// </summary>
[Authorize]
[PanelPermission("system.view")]
public class PointsController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<PointsController> _logger;

    public PointsController(IHttpClientFactory factory, IConfiguration config, ILogger<PointsController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 规则列表页
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.GetAsync($"{apiBase}/api/admin/points/rules");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    ViewBag.Items = JsonSerializer.Deserialize<List<PointRuleDto>>(data.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    return View();
                }
            }
            TempData["Error"] = $"规则加载失败: {resp.StatusCode}";
            ViewBag.Items = new List<PointRuleDto>();
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载积分规则失败");
            TempData["Error"] = $"加载失败: {ex.Message}";
            ViewBag.Items = new List<PointRuleDto>();
            return View();
        }
    }

    /// <summary>
    /// 批量保存规则（逐条代理 API PUT /api/admin/points/rules/{id}；阶梯空串=取消阶梯）
    /// 改动说明：原单条 UpdateRule 的 form 嵌 tbody 为非法 HTML，改整表单批量提交
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRules(List<PointRuleSave> items)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var failed = 0;
        try
        {
            foreach (var item in items)
            {
                // 空串=清除阶梯（API 侧区分 null 不修改 / "" 清除）
                var ladder = item.LadderJson?.Trim() ?? "";
                var resp = await client.PutAsJsonAsync($"{apiBase}/api/admin/points/rules/{item.Id}",
                    new { amount = item.Amount, dailyLimit = item.DailyLimit, ladderJson = ladder, isEnabled = item.IsEnabled });
                if (!resp.IsSuccessStatusCode) failed++;
            }
            TempData[failed == 0 ? "Success" : "Error"] =
                failed == 0 ? "规则已保存，实时生效" : $"{failed} 条规则保存失败";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存积分规则失败");
            TempData["Error"] = $"保存失败: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// 规则保存表单行（绑定 Razor 的 items[i].Xxx 字段）
    /// </summary>
    public class PointRuleSave
    {
        /// <summary>规则 ID</summary>
        public Guid Id { get; set; }
        /// <summary>基础分值</summary>
        public int Amount { get; set; }
        /// <summary>每日上限（0=不限）</summary>
        public int DailyLimit { get; set; }
        /// <summary>连续阶梯 JSON（空=固定分值）</summary>
        public string? LadderJson { get; set; }
        /// <summary>是否启用</summary>
        public bool IsEnabled { get; set; }
    }
}
