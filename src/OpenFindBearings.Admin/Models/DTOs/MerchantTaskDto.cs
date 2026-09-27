namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 商家集体任务定义 DTO（v2.6.0 M3，对齐 API /api/admin/points/merchant-tasks 响应）
/// </summary>
public class MerchantTaskDto
{
    /// <summary>任务 ID（更新提交用）</summary>
    public Guid Id { get; set; }

    /// <summary>任务键（全局唯一，锚定台账与奖励 bizId，创建后不可变）</summary>
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>任务名（成员可见文案）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>任务描述</summary>
    public string? Description { get; set; }

    /// <summary>指标键（corrections/treasury/products）</summary>
    public string MetricKey { get; set; } = string.Empty;

    /// <summary>达标目标值</summary>
    public int TargetValue { get; set; }

    /// <summary>周期（1 周 / 2 月）</summary>
    public int Period { get; set; }

    /// <summary>奖励对象（1 全体成员 / 2 金库）</summary>
    public int RewardType { get; set; }

    /// <summary>奖励分值</summary>
    public int RewardAmount { get; set; }

    /// <summary>是否启用</summary>
    public bool Enabled { get; set; }

    /// <summary>排序权重（小者靠前）</summary>
    public int SortOrder { get; set; }
}
