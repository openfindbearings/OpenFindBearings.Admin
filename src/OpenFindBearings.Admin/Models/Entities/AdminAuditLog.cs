namespace OpenFindBearings.Admin.Models.Entities;

/// <summary>
/// 管理后台审计日志，记录所有管理操作
/// </summary>
public class AdminAuditLog
{
    /// <summary>主键</summary>
    public Guid Id { get; set; }

    /// <summary>操作用户 ID</summary>
    public Guid? UserId { get; set; }

    /// <summary>操作用户名</summary>
    public string? Username { get; set; }

    /// <summary>操作类型，对应 AdminAction 枚举值</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>操作资源类型，如 Brand / Merchant / Bearing 等</summary>
    public string? ResourceType { get; set; }

    /// <summary>操作资源 ID</summary>
    public string? ResourceId { get; set; }

    /// <summary>操作详情描述</summary>
    public string? Detail { get; set; }

    /// <summary>操作结果：Success / Failure</summary>
    public string Result { get; set; } = "Success";

    /// <summary>操作者 IP 地址</summary>
    public string? IpAddress { get; set; }

    /// <summary>操作时间（UTC）</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
