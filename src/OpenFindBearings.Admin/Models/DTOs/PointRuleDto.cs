namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 积分赚分规则 DTO（v1.26.0，对齐 API /api/admin/points/rules 响应）
/// </summary>
public class PointRuleDto
{
    /// <summary>规则 ID（更新提交用）</summary>
    public Guid Id { get; set; }

    /// <summary>动作类型（daily_login/daily_checkin/…）</summary>
    public string GrantType { get; set; } = string.Empty;

    /// <summary>动作中文名</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>基础分值（有阶梯时为起步值）</summary>
    public int Amount { get; set; }

    /// <summary>每日上限（0=不限）</summary>
    public int DailyLimit { get; set; }

    /// <summary>连续阶梯 JSON 数组（如 [2,3,4,5,5]，null=固定分值）</summary>
    public string? LadderJson { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>规则说明</summary>
    public string? Description { get; set; }
}
