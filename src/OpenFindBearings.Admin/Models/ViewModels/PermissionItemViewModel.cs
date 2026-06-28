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
    /// 是否已授权
    /// </summary>
    public bool Granted { get; set; }
}
