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

    // 改动说明：输出 UTC ISO 8601 字符串，由前端 data-utc JS 按浏览器时区显示
    public string CreatedAtUtc => CreatedAt == DateTime.MinValue ? "" : CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ");
    public string CreatedAtFallback => CreatedAt == DateTime.MinValue ? "-" : CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ");
}

public class PendingReviewListViewModel
{
    public List<PendingReviewItemViewModel> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? CurrentFilter { get; set; }
}
