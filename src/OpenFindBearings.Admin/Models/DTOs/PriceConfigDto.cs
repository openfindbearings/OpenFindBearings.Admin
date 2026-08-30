namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 价格配置 DTO，对应 API /api/admin/config/price 端点
/// </summary>
public class PriceConfigDto
{
    /// <summary>
    /// 默认价格可见性（Public / LoginRequired）
    /// </summary>
    public string DefaultVisibility { get; set; } = "LoginRequired";

    /// <summary>
    /// 是否显示议价标签
    /// </summary>
    public bool ShowNegotiableLabel { get; set; } = true;

    /// <summary>
    /// 是否启用数值化价格用于排序
    /// </summary>
    public bool NumericForSorting { get; set; } = true;

    /// <summary>
    /// 价格提取正则表达式
    /// </summary>
    public string ExtractPattern { get; set; } = @"¥(\d+(?:\.\d+)?)";
}
