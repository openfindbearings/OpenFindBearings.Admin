using System.Text.Json;
using OpenFindBearings.Admin.Helpers;
using System.Text.Json.Serialization;
using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 商品审核控制器（v2.15.0）：商家上架商品的人工审核出口。
/// 待审列表服务端渲染（对齐入驻审批/纠错审核页），通过/驳回由浏览器调用
/// Program.cs 注册的代理端点 /api/proxy/merchant-bearings/... 完成。
/// 权限 merchant.verify（与挂礼审核、入驻审批同口径）。
/// </summary>
[Authorize]
[PanelPermission("merchant.verify")]
public class MerchantBearingReviewController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<MerchantBearingReviewController> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public MerchantBearingReviewController(IHttpClientFactory factory, IConfiguration config, ILogger<MerchantBearingReviewController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    /// <summary>API 基址配置（与各控制器同口径）</summary>
    private string ApiBase() => ApiUrlResolver.Api(_config);

    /// <summary>
    /// 待审核商品列表页：拉取 API /api/admin/merchant-bearings/pending，服务端渲染首屏与分页。
    /// API 不可达时降级空表并提示，不阻断页面。
    /// </summary>
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
    {
        var client = _factory.CreateClient("ApiClient");
        var url = $"{ApiBase()}/api/admin/merchant-bearings/pending?page={page}&pageSize={pageSize}";
        try
        {
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("待审核商品 API 请求失败: {Status}", resp.StatusCode);
                ViewBag.Error = "无法获取待审核商品列表";
            }
            else
            {
                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<PendingMerchantBearingRowDto>>>(json, JsonOpts);
                ViewBag.Items = apiResp?.Data?.Items ?? [];
                ViewBag.TotalCount = apiResp?.Data?.TotalCount ?? 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取待审核商品列表异常");
            ViewBag.Error = "连接 API 服务异常";
        }

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }
}

/// <summary>
/// 待审核商品行 DTO，字段对齐 API PendingMerchantBearingDto（camelCase 序列化）
/// </summary>
public class PendingMerchantBearingRowDto
{
    /// <summary>商家商品关联 ID（审核动作以此为准）</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }

    /// <summary>商家信息</summary>
    [JsonPropertyName("merchant")] public PendingMerchantRefDto? Merchant { get; set; }

    /// <summary>轴承信息</summary>
    [JsonPropertyName("bearing")] public PendingBearingRefDto? Bearing { get; set; }

    /// <summary>价格描述（商家自填）</summary>
    [JsonPropertyName("priceDescription")] public string? PriceDescription { get; set; }

    /// <summary>库存描述</summary>
    [JsonPropertyName("stockDescription")] public string? StockDescription { get; set; }

    /// <summary>最小起订量描述</summary>
    [JsonPropertyName("minOrderDescription")] public string? MinOrderDescription { get; set; }

    /// <summary>商家备注</summary>
    [JsonPropertyName("remarks")] public string? Remarks { get; set; }

    /// <summary>提交时间（UTC，展示层转本地）</summary>
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }

    /// <summary>提交人（商家员工）</summary>
    [JsonPropertyName("submitterName")] public string? SubmitterName { get; set; }
}

/// <summary>待审核商品所属商家精简信息</summary>
public class PendingMerchantRefDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("companyName")] public string? CompanyName { get; set; }
}

/// <summary>待审核商品对应轴承精简信息</summary>
public class PendingBearingRefDto
{
    [JsonPropertyName("partNumber")] public string? PartNumber { get; set; }
    [JsonPropertyName("oldNumber")] public string? OldNumber { get; set; }
    [JsonPropertyName("brandName")] public string? BrandName { get; set; }
    [JsonPropertyName("bearingTypeName")] public string? BearingTypeName { get; set; }
    [JsonPropertyName("innerDiameter")] public decimal InnerDiameter { get; set; }
    [JsonPropertyName("outerDiameter")] public decimal OuterDiameter { get; set; }
    [JsonPropertyName("width")] public decimal Width { get; set; }
}