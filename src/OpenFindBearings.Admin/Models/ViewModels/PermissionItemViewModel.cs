namespace OpenFindBearings.Admin.Models.ViewModels;

/// <summary>
/// 权限项 ViewModel
/// </summary>
public class PermissionItemViewModel
{
    /// <summary>
    /// 权限键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 权限中文描述（API 权限表 Description 直读）
    /// 改动说明（v1.30.0 权限目录重排）：展示名以数据库为单一事实源，
    /// 视图不再维护逐键 DisplayName 长 switch（37 键双份维护必漂移）
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否已授权
    /// </summary>
    public bool Granted { get; set; }
}
