using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Models.ViewModels;

public record BearingListViewModel
{
    public List<BearingItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public bool IncludeDeleted { get; set; }

    /// <summary>
    /// 品牌下拉数据（新建轴承弹窗用）
    /// 改动说明（v1.49.1 去建档可用性修复）：API 建档强制要求 BrandId/BearingTypeId 与三项尺寸，
    /// 原弹窗只有文本输入且不提交这些字段，保存必然失败
    /// </summary>
    public List<BrandItemDto> Brands { get; set; } = [];

    /// <summary>轴承类型下拉数据（新建轴承弹窗用，同上）</summary>
    public List<BearingTypeItemDto> BearingTypes { get; set; } = [];

    /// <summary>创建起始日期（业务日历 yyyy-MM-dd，含）——改动说明（v1.40.0 下钻筛选）</summary>
    public string? DateFrom { get; set; }

    /// <summary>创建截止日期（业务日历 yyyy-MM-dd，含）</summary>
    public string? DateTo { get; set; }

    /// <summary>来源筛选：internet=仅互联网 / noninternet=仅非互联网 / 空=不限</summary>
    public string? SourceFilter { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
