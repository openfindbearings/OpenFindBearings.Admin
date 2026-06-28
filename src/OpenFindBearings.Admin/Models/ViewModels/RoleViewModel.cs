namespace OpenFindBearings.Admin.Models.ViewModels;

/// <summary>
/// 角色列表项 ViewModel
/// </summary>
public class RoleViewModel
{
    /// <summary>
    /// 角色名称
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// 已授权权限数量
    /// </summary>
    public int PermissionCount { get; set; }
}
