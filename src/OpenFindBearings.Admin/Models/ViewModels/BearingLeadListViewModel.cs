using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Models.ViewModels;

/// <summary>
/// 型号线索列表视图模型
/// 承载分页数据与当前筛选条件（状态 / 关键词），供线索页渲染与翻页保留
/// </summary>
public record BearingLeadListViewModel
{
    /// <summary>当前页线索</summary>
    public List<BearingLeadItemDto> Items { get; set; } = [];

    /// <summary>总条数</summary>
    public int TotalCount { get; set; }

    /// <summary>当前页码</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>状态筛选：pending/ignored/resolved，空=全部</summary>
    public string? Status { get; set; }

    /// <summary>型号关键词</summary>
    public string? Search { get; set; }

    /// <summary>总页数</summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
