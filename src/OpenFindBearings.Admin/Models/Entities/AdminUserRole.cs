namespace OpenFindBearings.Admin.Models.Entities;

/// <summary>
/// 管理后台用户角色分配，一个用户可分配多个角色
/// </summary>
public class AdminUserRole
{
    /// <summary>主键</summary>
    public Guid Id { get; set; }

    /// <summary>用户 ID（Identity 系统的用户标识）</summary>
    public Guid UserId { get; set; }

    /// <summary>角色名称，如 admin / editor / viewer</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>分配时间（UTC）</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
