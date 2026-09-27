using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 成就目录管理控制器（v2.1.0 成就子系统）：代理 API /api/admin/achievements。
/// 列表 system.view 可见；编辑/启停 system.manage（复用既有键，免新增权限迁移）
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

    /// <summary>成就目录列表页（按范围/分类分组展示，含停用项）</summary>
    public async Task<IActionResult> Index()
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.GetAsync($"{apiBase}/api/admin/achievements");
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var d))
                {
                    var items = d.EnumerateArray().Select(x => new AchievementRowVm
                    {
                        Id = x.GetProperty("id").GetGuid(),
                        Key = x.GetProperty("key").GetString() ?? "",
                        Name = x.GetProperty("name").GetString() ?? "",
                        Description = x.GetProperty("description").GetString() ?? "",
                        Scope = x.GetProperty("scope").GetInt32(),
                        Category = x.GetProperty("category").GetString() ?? "",
                        MetricKey = x.GetProperty("metricKey").GetString() ?? "",
                        ProgressTarget = x.GetProperty("progressTarget").GetInt32(),
                        MetaPoints = x.GetProperty("metaPoints").GetInt32(),
                        RewardPoints = x.GetProperty("rewardPoints").GetInt32(),
                        TitleReward = x.TryGetProperty("titleReward", out var tr) && tr.ValueKind == JsonValueKind.String ? tr.GetString() : null,
                        ImageKey = x.TryGetProperty("imageKey", out var ik) && ik.ValueKind == JsonValueKind.String ? ik.GetString() : null,
                        Rare = x.GetProperty("rare").GetBoolean(),
                        Hidden = x.GetProperty("hidden").GetBoolean(),
                        Enabled = x.GetProperty("enabled").GetBoolean()
                    }).ToList();
                    return View(items);
                }
            }
            TempData["Error"] = $"成就目录加载失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "成就目录拉取异常");
            TempData["Error"] = $"成就目录拉取失败: {ex.Message}";
        }
        return View(new List<AchievementRowVm>());
    }

    /// <summary>编辑成就（分值/阈值/称号/启停/勋章图键）</summary>
    [HttpPost]
    [PanelPermission("system.manage")]
    public async Task<IActionResult> Update(Guid id, string name, string description, int progressTarget,
        int metaPoints, int rewardPoints, string? titleReward, bool enabled, string? imageKey)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var payload = new { name, description, progressTarget, metaPoints, rewardPoints, titleReward, enabled, imageKey };
            var resp = await client.PutAsJsonAsync($"{apiBase}/api/admin/achievements/{id}", payload);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] =
                resp.IsSuccessStatusCode ? "成就已更新" : $"更新失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"更新失败: {ex.Message}";
        }
        return RedirectToAction("Index");
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
    public int RewardPoints { get; set; }
    public string? TitleReward { get; set; }
    public string? ImageKey { get; set; }
    public bool Rare { get; set; }
    public bool Hidden { get; set; }
    public bool Enabled { get; set; }
}
