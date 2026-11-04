using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Models.ViewModels;

public record MerchantListViewModel
{
    public List<MerchantItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? VerifiedOnly { get; set; }
    public bool IncludeDeleted { get; set; }

    /// <summary>
    /// 时间筛选字段：registered=注册时间（CreatedAt）/ onboarded=入驻时间（OnboardedAt）
    /// 改动说明（v1.40.0 时间/来源筛选）
    /// </summary>
    public string? DateField { get; set; }

    /// <summary>时间筛选起始日期（业务日历 yyyy-MM-dd，含）</summary>
    public string? DateFrom { get; set; }

    /// <summary>时间筛选截止日期（业务日历 yyyy-MM-dd，含）</summary>
    public string? DateTo { get; set; }

    /// <summary>来源筛选：internet=仅互联网 / noninternet=仅非互联网 / 空=不限</summary>
    public string? SourceFilter { get; set; }

    /// <summary>排序字段（registeredat=注册时间 / onboardedat=入驻时间，空=默认认证优先）</summary>
    public string? SortBy { get; set; }

    /// <summary>排序方向（asc/desc）</summary>
    public string? SortOrder { get; set; }

    /// <summary>牌级申诉来源商家 Id（从纠错页「去调级」跳入时有值，v2.21.0 牌级整合）</summary>
    public Guid? AppealMerchantId { get; set; }

    /// <summary>牌级申诉来源商家名称（仅用于提示条展示）</summary>
    public string? AppealMerchantName { get; set; }

    /// <summary>牌级申诉理由（预填调级弹窗的原因，仍可编辑）</summary>
    public string? AppealReason { get; set; }

    /// <summary>申诉建议牌级（预选目标档位，未认证时前端仍禁用铜牌及以上）</summary>
    public int? AppealGrade { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
