namespace OpenFindBearings.Admin.Helpers;

/// <summary>
/// 商户类型 / 入驻渠道枚举 → 中文（v2.13.2 收敛）：原入驻审批页与商家数据页各写一份 C# 映射，
/// 详情抽屉又各写一份 JS 映射（含大小写归一差异）；此处集中为唯一来源，JS 侧经序列化下发。
/// </summary>
public static class MerchantLabels
{
    /// <summary>类型枚举名（Manufacturer/AuthorizedDealer/Distributor/Trader）→ 中文</summary>
    public static string TypeName(string? t) => t switch
    {
        "Manufacturer" => "生产厂家",
        "AuthorizedDealer" => "授权经销商",
        "Distributor" => "分销商",
        "Trader" => "贸易商",
        _ => t ?? "-"
    };

    /// <summary>入驻渠道码（ApplicationMode：Self/Claim/Nomination/None）→ 中文</summary>
    public static string ModeLabel(string? mode) => mode switch
    {
        "Self" => "自助新建",
        "Claim" => "认领",
        "Nomination" => "提名",
        "None" or null or "" => "-",
        _ => mode ?? "-"
    };

    /// <summary>类型枚举名 → 中文（前端 JS 用，视图序列化下发）</summary>
    public static readonly Dictionary<string, string> TypeMap = new()
    {
        ["Manufacturer"] = "生产厂家",
        ["AuthorizedDealer"] = "授权经销商",
        ["Distributor"] = "分销商",
        ["Trader"] = "贸易商"
    };

    /// <summary>入驻渠道小写码 → 中文（前端 JS 用；抽屉已大小写归一后再查此表）</summary>
    public static readonly Dictionary<string, string> ModeMapLower = new()
    {
        ["self"] = "自助新建",
        ["claim"] = "认领",
        ["nomination"] = "提名",
        ["none"] = "-"
    };
}