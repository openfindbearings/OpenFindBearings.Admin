using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Models.ViewModels;

public record MerchantListViewModel
{
    public List<MerchantItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public bool IncludeDeleted { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
