using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 积分任务保存控制器（v1.28.0 改版：原独立列表页 Index 并入系统配置页"积分任务"tab，
/// 由 ConfigController 拉取渲染；本控制器仅保留批量保存端点，代理 API PUT /api/admin/points/rules/{id}）
/// </summary>
[Authorize]
[PanelPermission("points.manage")]
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

    /// <summary>
    /// 批量保存段位档位（v2.12.0 等级玩法：逐条代理 API PUT /api/admin/points/levels/{id}；
    /// Level 号不在提交字段内——落档与升档礼幂等键锚定它不可改）
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLevels(List<PointLevelSave> items)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var failed = 0;
        try
        {
            foreach (var item in items)
            {
                var resp = await client.PutAsJsonAsync($"{apiBase}/api/admin/points/levels/{item.Id}",
                    new
                    {
                        minTotalEarned = item.MinTotalEarned,
                        name = item.Name?.Trim(),
                        levelUpBonus = item.LevelUpBonus,
                        enabled = item.Enabled
                    });
                if (!resp.IsSuccessStatusCode) failed++;
            }
            TempData[failed == 0 ? "Success" : "Error"] =
                failed == 0 ? "档位已保存，实时生效" : $"{failed} 条档位保存失败";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存段位档位失败");
            TempData["Error"] = $"保存失败: {ex.Message}";
        }
        return RedirectToAction("Index", "Config", new { tab = "levels" });
    }

    /// <summary>
    /// 段位档位保存表单行（Razor items[i].Xxx 绑定；Level 号只读展示不提交）
    /// </summary>
    public class PointLevelSave
    {
        /// <summary>档位 ID</summary>
        public Guid Id { get; set; }
        /// <summary>进入该档最低累计获得轴承币</summary>
        public int MinTotalEarned { get; set; }
        /// <summary>段位名</summary>
        public string? Name { get; set; }
        /// <summary>升档礼轴承币（0=不发）</summary>
        public int LevelUpBonus { get; set; }
        /// <summary>是否启用</summary>
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// 批量保存商家集体任务定义（v2.6.0 M3：逐条代理 API PUT /api/admin/points/merchant-tasks/{id}；
    /// TaskKey 不在提交字段内——锚定台账不可变）
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMerchantTasks(List<MerchantTaskSave> items)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var failed = 0;
        try
        {
            foreach (var item in items)
            {
                var resp = await client.PutAsJsonAsync($"{apiBase}/api/admin/points/merchant-tasks/{item.Id}",
                    new
                    {
                        name = item.Name,
                        description = item.Description ?? "",
                        metricKey = item.MetricKey,
                        targetValue = item.TargetValue,
                        period = item.Period,
                        rewardType = item.RewardType,
                        rewardAmount = item.RewardAmount,
                        enabled = item.Enabled,
                        sortOrder = item.SortOrder
                    });
                if (!resp.IsSuccessStatusCode) failed++;
            }
            TempData[failed == 0 ? "Success" : "Error"] =
                failed == 0 ? "任务已保存，实时生效" : $"{failed} 条任务保存失败";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存商家集体任务失败");
            TempData["Error"] = $"保存失败: {ex.Message}";
        }
        return RedirectToAction("Index", "Config", new { tab = "merchattasks" });
    }

    /// <summary>
    /// 新建商家集体任务（v2.6.0 M3：代理 API POST /api/admin/points/merchant-tasks；TaskKey 唯一性由 API 校验）
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMerchantTask(MerchantTaskCreate form)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsJsonAsync($"{apiBase}/api/admin/points/merchant-tasks",
                new
                {
                    taskKey = form.TaskKey?.Trim(),
                    name = form.Name?.Trim(),
                    description = form.Description?.Trim() ?? "",
                    metricKey = form.MetricKey,
                    targetValue = form.TargetValue,
                    period = form.Period,
                    rewardType = form.RewardType,
                    rewardAmount = form.RewardAmount,
                    sortOrder = form.SortOrder
                });
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode
                ? "任务已创建"
                : $"创建失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新建商家集体任务失败");
            TempData["Error"] = $"创建失败: {ex.Message}";
        }
        return RedirectToAction("Index", "Config", new { tab = "merchattasks" });
    }

    /// <summary>
    /// 任务保存表单行（Razor items[i].Xxx 绑定；TaskKey 只读展示不提交）
    /// </summary>
    public class MerchantTaskSave
    {
        /// <summary>任务 ID</summary>
        public Guid Id { get; set; }
        /// <summary>任务名</summary>
        public string? Name { get; set; }
        /// <summary>任务描述</summary>
        public string? Description { get; set; }
        /// <summary>指标键（corrections/treasury/products）</summary>
        public string? MetricKey { get; set; }
        /// <summary>达标目标值</summary>
        public int TargetValue { get; set; }
        /// <summary>周期（1 周 / 2 月）</summary>
        public int Period { get; set; }
        /// <summary>奖励对象（1 成员 / 2 金库）</summary>
        public int RewardType { get; set; }
        /// <summary>奖励分值</summary>
        public int RewardAmount { get; set; }
        /// <summary>是否启用</summary>
        public bool Enabled { get; set; }
        /// <summary>排序权重</summary>
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// 任务新建表单（单条提交，字段与批量保存行一致外加 TaskKey）
    /// </summary>
    public class MerchantTaskCreate
    {
        /// <summary>任务键（全局唯一，创建后不可变）</summary>
        public string? TaskKey { get; set; }
        /// <summary>任务名</summary>
        public string? Name { get; set; }
        /// <summary>任务描述</summary>
        public string? Description { get; set; }
        /// <summary>指标键</summary>
        public string? MetricKey { get; set; }
        /// <summary>达标目标值</summary>
        public int TargetValue { get; set; }
        /// <summary>周期</summary>
        public int Period { get; set; } = 1;
        /// <summary>奖励对象</summary>
        public int RewardType { get; set; } = 1;
        /// <summary>奖励分值</summary>
        public int RewardAmount { get; set; }
        /// <summary>排序权重</summary>
        public int SortOrder { get; set; }
    }
}
