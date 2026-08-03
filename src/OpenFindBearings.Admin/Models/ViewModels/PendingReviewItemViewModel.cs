namespace OpenFindBearings.Admin.Models.ViewModels;

public class PendingReviewItemViewModel
{
    public Guid Id { get; set; }
    public string FieldType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string OriginalValue { get; set; } = string.Empty;
    public string? SuggestedValue { get; set; }
    public int? Confidence { get; set; }
    public DateTime CreatedAt { get; set; }

    public string EntityTypeDisplay => EntityType switch
    {
        "brand" => "品牌",
        "bearing_type" => "类型",
        "bearing" => "轴承",
        "merchant" => "商家",
        "interchange" => "替代品",
        "merchant_bearing" => "商家商品",
        _ => EntityType
    };

    public int? ConfidenceDisplay => Confidence;

    public string CreatedAtDisplay => CreatedAt == DateTime.MinValue ? "-" : CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

public class PendingReviewListViewModel
{
    public List<PendingReviewItemViewModel> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? CurrentFilter { get; set; }
}
