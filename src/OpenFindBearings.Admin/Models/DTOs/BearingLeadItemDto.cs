namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 型号线索项（对齐 API BearingLeadDto）
/// 用于管理端展示"平台型号库尚未收录"的型号及其累积证据与处理状态
/// </summary>
public class BearingLeadItemDto
{
    /// <summary>线索 ID</summary>
    public Guid Id { get; set; }

    /// <summary>型号原文</summary>
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>期望品牌（可空，仅发布寻货会带）</summary>
    public string? Brand { get; set; }

    /// <summary>累计出现次数</summary>
    public int OccurrenceCount { get; set; }

    /// <summary>最近一次来源（Demand=发布寻货 / Response=商家应答）</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>来源中文展示名</summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>首次出现时间（UTC）</summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>最近出现时间（UTC）</summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>状态（Pending=待建档 / Ignored=已忽略 / Resolved=已入库）</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>状态中文展示名</summary>
    public string StatusText { get; set; } = string.Empty;
}
