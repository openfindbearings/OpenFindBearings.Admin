using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.Enums;

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
        var permissions = Enum.GetValues<PermissionKey>()
            .Select(p => new PermissionDisplayViewModel
            {
                Key = p.ToString(),
                DisplayName = GetDisplayName(p.ToString()),
                Group = GetGroup(p.ToString())
            })
            .ToList();

        ViewBag.Groups = permissions.GroupBy(p => p.Group).OrderBy(g => g.Key).ToList();
        return View();
    }

    private static string GetGroup(string key) => key switch
    {
        "DashboardView" => "仪表盘",
        "BearingView" or "BearingCreate" or "BearingEdit" or "BearingDelete" => "轴承管理",
        "MerchantView" or "MerchantManage" or "MerchantVerify" => "商家管理",
        "CorrectionReview" => "审核管理",
        "EtlManage" or "CrawlerManage" => "同步与爬虫",
        "RoleManage" or "UserManage" => "认证管理",
        "SystemView" or "SystemManage" => "系统配置",
        "AuditView" => "审计日志",
        "DataRestore" => "数据操作",
        _ => "其他"
    };

    private static string GetDisplayName(string key) => key switch
    {
        "DashboardView" => "查看仪表盘",
        "BearingView" => "查看轴承",
        "BearingCreate" => "创建轴承",
        "BearingEdit" => "编辑轴承",
        "BearingDelete" => "删除轴承",
        "MerchantView" => "查看商家",
        "MerchantManage" => "管理商家",
        "MerchantVerify" => "认证审核",
        "CorrectionReview" => "纠错审核",
        "EtlManage" => "任务管理",
        "CrawlerManage" => "爬虫管理",
        "RoleManage" => "角色管理",
        "UserManage" => "用户管理",
        "SystemView" => "查看系统配置",
        "SystemManage" => "管理配置",
        "AuditView" => "查看审计日志",
        "DataRestore" => "恢复已删除数据",
        _ => key
    };
}

public class PermissionDisplayViewModel
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
}
