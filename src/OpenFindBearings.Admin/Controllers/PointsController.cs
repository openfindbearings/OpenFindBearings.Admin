using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 积分任务保存控制器（v1.28.0 改版：原独立列表页 Index 并入系统配置页"积分任务"tab，
/// 由 ConfigController 拉取渲染；本控制器仅保留批量保存端点，代理 API PUT /api/admin/points/rules/{id}）
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
    /// 批量保存规则（逐条代理 API PUT /api/admin/points/rules/{id}；阶梯空串=取消阶梯）
    /// 改动说明：原单条 UpdateRule 的 form 嵌 tbody 为非法 HTML，改整表单批量提交；
    ///           v1.28.0 保存后回跳系统配置页的积分任务 tab（原回跳的本页 Index 已删）
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
        return RedirectToAction("Index", "Config", new { tab = "points" });
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
