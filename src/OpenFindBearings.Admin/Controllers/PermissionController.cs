using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.Constants;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 权限列表控制器（只读展示，由 PermissionKey 枚举驱动）
/// </summary>
[Authorize]
public class PermissionController : Controller
{
    /// <summary>
    /// 权限列表（只读展示）
    /// </summary>
    public IActionResult Index()
    {
        var permissions = PermissionKeys.All
            .Select(k => new PermissionDisplayViewModel
            {
                Key = k,
                DisplayName = GetDisplayName(k),
                Group = GetGroup(k)
            })
            .ToList();

        ViewBag.Groups = permissions.GroupBy(p => p.Group).OrderBy(g => g.Key).ToList();
        return View();
    }

    private static string GetGroup(string key) => key switch
    {
        "dashboard.view" => "仪表盘",
        "bearing.view" or "bearing.create" or "bearing.edit" or "bearing.delete" => "轴承管理",
        "merchant.view" or "merchant.manage" or "merchant.verify" => "商家管理",
        "correction.review" => "审核管理",
        "etl.manage" => "任务管理",
        "role.manage" or "user.manage" => "认证管理",
        "system.view" or "system.manage" => "系统配置",
        "audit.view" => "审计日志",
        "data.restore" or "data.harddelete" => "数据操作",
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
        "merchant.manage" => "管理商家",
        "merchant.verify" => "认证审核",
        "correction.review" => "纠错审核",
        "etl.manage" => "任务管理",
        "role.manage" => "角色管理",
        "user.manage" => "用户管理",
        "system.view" => "查看系统配置",
        "system.manage" => "管理配置",
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
