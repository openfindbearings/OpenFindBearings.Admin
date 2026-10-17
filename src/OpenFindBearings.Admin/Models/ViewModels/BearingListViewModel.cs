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

    /// <summary>创建起始日期（业务日历 yyyy-MM-dd，含）——改动说明（v1.40.0 下钻筛选）</summary>
    public string? DateFrom { get; set; }

    /// <summary>创建截止日期（业务日历 yyyy-MM-dd，含）</summary>
    public string? DateTo { get; set; }

    /// <summary>来源筛选：internet=仅互联网 / noninternet=仅非互联网 / 空=不限</summary>
    public string? SourceFilter { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
