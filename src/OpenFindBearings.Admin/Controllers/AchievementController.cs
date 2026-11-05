using OpenFindBearings.Admin.Authorization;
using OpenFindBearings.Admin.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 成就目录管理控制器（v2.1.0 成就子系统）：代理 API /api/admin/achievements。
/// 列表已并入系统配置页（v2.22.0），本控制器保留编辑提交与旧链接跳转。
/// </summary>
[Authorize]
[PanelPermission("system.view")]
public class AchievementController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<AchievementController> _logger;

    public AchievementController(IHttpClientFactory factory, IConfiguration config, ILogger<AchievementController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 旧独立列表页入口（v2.22.0 菜单收敛）：列表已并入系统配置的"勋章管理"tab，
    /// 此动作只做兼容跳转，保留旧书签与旧链接可用
    /// </summary>
    public IActionResult Index() => RedirectToAction("Index", "Config", new { tab = "achievements" });

    /// <summary>编辑成就（阈值/称号/启停/勋章图键；v2.12.0 起不含发币字段——成就纯荣誉化）</summary>
    [HttpPost]
    [PanelPermission("system.manage")]
    public async Task<IActionResult> Update(Guid id, string name, string description, int progressTarget,
        int metaPoints, string? titleReward, bool enabled, string? imageKey)
    {
        var apiBase = ApiUrlResolver.Api(_config);
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var payload = new { name, description, progressTarget, metaPoints, titleReward, enabled, imageKey };
            var resp = await client.PutAsJsonAsync($"{apiBase}/api/admin/achievements/{id}", payload);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] =
                resp.IsSuccessStatusCode ? "成就已更新" : $"更新失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"更新失败: {ex.Message}";
        }
        // 改动说明（v2.22.0 菜单收敛）：保存后回到系统配置的勋章管理 tab
        return RedirectToAction("Index", "Config", new { tab = "achievements" });
    }
}

/// <summary>成就目录行视图模型</summary>
public class AchievementRowVm
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Scope { get; set; }
    public string Category { get; set; } = "";
    public string MetricKey { get; set; } = "";
    public int ProgressTarget { get; set; }
    public int MetaPoints { get; set; }
    public string? TitleReward { get; set; }
    public string? ImageKey { get; set; }
    public bool Rare { get; set; }
    public bool Hidden { get; set; }
    public bool Enabled { get; set; }
}
