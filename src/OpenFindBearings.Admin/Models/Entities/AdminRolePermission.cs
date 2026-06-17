namespace OpenFindBearings.Admin.Models.Entities;

/// <summary>
/// 管理后台角色权限映射，定义每个角色可执行的操作
/// </summary>
public class AdminRolePermission
{
    /// <summary>主键</summary>
    public Guid Id { get; set; }

    /// <summary>角色名称，如 admin / editor / viewer</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>权限键，对应 PermissionKey 枚举值</summary>
    public string PermissionKey { get; set; } = string.Empty;

    /// <summary>是否授权，true 表示授予该权限</summary>
    public bool Granted { get; set; } = true;

    /// <summary>创建时间（UTC）</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
