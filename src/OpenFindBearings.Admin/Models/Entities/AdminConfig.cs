namespace OpenFindBearings.Admin.Models.Entities;

/// <summary>
/// 管理后台本地配置键值对，存储 Admin 项目的独立配置
/// </summary>
public class AdminConfig
{
    /// <summary>主键</summary>
    public Guid Id { get; set; }

    /// <summary>配置键（唯一），如 Sidebar.Theme / PageSize.Default</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>配置值，以 JSON 或字符串形式存储</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>配置描述</summary>
    public string? Description { get; set; }

    /// <summary>最后更新时间（UTC），null 表示使用默认值</summary>
    public DateTime? UpdatedAt { get; set; }
}
