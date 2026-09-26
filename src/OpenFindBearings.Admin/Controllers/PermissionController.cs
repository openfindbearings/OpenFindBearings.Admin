using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 权限列表控制器（只读展示）。
/// 改动说明（v1.25.0）：权限点清单改从 API 权限表拉取（唯一事实源），本地常量目录废弃——
/// 界面新增权限点无意义（权限点=端点过滤器代码常量），此页仅展示与分组
/// </summary>
[Authorize]
[PanelPermission("permission.view")]
public class PermissionController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    /// <summary>
    /// 构造：注入 HTTP 工厂与配置
    /// </summary>
    public PermissionController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    private string ApiBase() => _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";

    /// <summary>
    /// 权限列表（只读展示，代理 API 权限目录）
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var permissions = new List<PermissionDisplayViewModel>();
        var resp = await _factory.CreateClient("ApiClient").GetAsync($"{ApiBase()}/api/admin/permissions?page=1&pageSize=200");
        if (resp.IsSuccessStatusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("items", out var items))
                {
                    permissions = items.EnumerateArray().Select(x =>
                    {
                        var key = x.GetProperty("name").GetString() ?? "";
                        return new PermissionDisplayViewModel
                        {
                            Key = key,
                            DisplayName = x.TryGetProperty("description", out var d) && !string.IsNullOrEmpty(d.GetString()) ? d.GetString()! : GetDisplayName(key),
                            Group = GetGroup(key)
                        };
                    }).ToList();
                }
            }
            catch (JsonException)
            {
                // 解析失败留空列表，视图有空态
            }
        }
        else
        {
            TempData["Error"] = $"权限目录加载失败: {resp.StatusCode}";
        }

        ViewBag.Groups = permissions.GroupBy(p => p.Group).OrderBy(g => g.Key).ToList();
        return View();
    }

    // 改动说明（v1.30.0 权限目录重排）：组名由键前缀推导（与三段式目录天然对齐），
    // 逐键列举的旧 switch 随目录膨胀必漂移，废弃
    private static string GetGroup(string key) => key.Split('.')[0] switch
    {
        "dashboard" => "仪表盘",
        "bearing" or "brand" or "type" => "数据管理",
        "merchant" or "mapping" => "商家与映射",
        "data" => "数据危险操作",
        "review" or "correction" or "sourcing" => "审核管理",
        "sync" => "任务管理",
        "user" or "role" or "permission" => "认证管理",
        "system" or "points" => "系统配置",
        "audit" => "审计日志",
        _ => "其他"
    };

    private static string GetDisplayName(string key) => key switch
    {
        "dashboard.view" => "查看仪表盘",
        "bearing.view" => "查看轴承",
        "bearing.create" => "创建轴承",
        "bearing.edit" => "编辑轴承",
        "bearing.delete" => "删除轴承",
        "merchant.view" => "查看商家",
        "merchant.manage" => "编辑商家",
        "merchant.detach" => "解除商户归属",
        "merchant.import" => "商家库存导入",
        "sourcing.view" => "查看寻货",
        "sourcing.manage" => "寻货治理（下架）",
        "merchant.verify" => "认证审核",
        "correction.review" => "纠错审核",
        "review.sync" => "同步数据审核",
        "role.manage" => "角色管理",
        "user.view" => "查看用户",
        "user.ban" => "封禁与解禁用户",
        "user.manage" => "管理用户账号",
        "user.assign" => "分配平台角色",
        "permission.view" => "查看权限清单",
        "system.view" => "查看系统配置",
        "system.manage" => "管理配置",
        "points.manage" => "配置积分任务",
        "sync.run" => "触发爬虫任务",
        "audit.view" => "查看审计日志",
        "data.restore" => "恢复已删除数据",
        "data.harddelete" => "彻底删除数据",
        _ => key
    };
}

public class PermissionDisplayViewModel
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
}
