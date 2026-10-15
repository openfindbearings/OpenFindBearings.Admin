using System.Text.Json;
using OpenFindBearings.Admin.Helpers;
using System.Text.Json.Serialization;
using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 替代关系管理控制器（v2.16.0 替代品治理）：平台建标准、商家报候选、平台审核生效。
/// 管理列表服务端渲染（对齐商品审核页），新建/编辑/删除/通过/驳回由浏览器调用
/// Program.cs 注册的代理端点 /api/proxy/admin-interchanges/... 完成。
/// 权限 merchant.verify（与商品审核、入驻审批同口径）。
/// </summary>
[Authorize]
[PanelPermission("merchant.verify")]
public class InterchangesController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<InterchangesController> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public InterchangesController(IHttpClientFactory factory, IConfiguration config, ILogger<InterchangesController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    /// <summary>API 基址配置（与各控制器同口径）</summary>
    private string ApiBase() => ApiUrlResolver.Api(_config);

    /// <summary>
    /// 替代关系管理列表页：拉取 API /api/admin/interchanges（支持关键字/审核状态过滤），服务端渲染首屏与分页。
    /// API 不可达时降级空表并提示，不阻断页面。
    /// </summary>
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string? keyword = null, int? reviewStatus = null)
    {
        var client = _factory.CreateClient("ApiClient");
        var url = $"{ApiBase()}/api/admin/interchanges?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(keyword))
            url += $"&keyword={Uri.EscapeDataString(keyword)}";
        if (reviewStatus.HasValue)
            url += $"&reviewStatus={reviewStatus.Value}";
        try
        {
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("替代关系列表 API 请求失败: {Status}", resp.StatusCode);
                ViewBag.Error = "无法获取替代关系列表";
            }
            else
            {
                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<InterchangeAdminRowDto>>>(json, JsonOpts);
                ViewBag.Items = apiResp?.Data?.Items ?? [];
                ViewBag.TotalCount = apiResp?.Data?.TotalCount ?? 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取替代关系列表异常");
            ViewBag.Error = "连接 API 服务异常";
        }

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Keyword = keyword;
        ViewBag.ReviewStatus = reviewStatus;
        return View();
    }
}

/// <summary>
/// 替代关系管理行 DTO，字段对齐 API InterchangeAdminDto（camelCase 序列化）。
/// 管理列表与审核队列共用。
/// </summary>
public class InterchangeAdminRowDto
{
    /// <summary>替代关系 ID（编辑/删除/审核动作以此为准）</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }

    /// <summary>源轴承（被替代方）精简信息</summary>
    [JsonPropertyName("sourceBearing")] public InterchangeBearingRefRowDto? SourceBearing { get; set; }

    /// <summary>目标轴承（替代方）精简信息</summary>
    [JsonPropertyName("targetBearing")] public InterchangeBearingRefRowDto? TargetBearing { get; set; }

    /// <summary>替代类型（exact/conditional/functional）</summary>
    [JsonPropertyName("interchangeType")] public string? InterchangeType { get; set; }

    /// <summary>替代类型中文标签</summary>
    [JsonPropertyName("typeLabel")] public string? TypeLabel { get; set; }

    /// <summary>可信度 0-100</summary>
    [JsonPropertyName("confidence")] public int Confidence { get; set; }

    /// <summary>来源说明（对应实体 Source 字段）</summary>
    [JsonPropertyName("origin")] public string? Origin { get; set; }

    /// <summary>备注</summary>
    [JsonPropertyName("remarks")] public string? Remarks { get; set; }

    /// <summary>是否双向替代</summary>
    [JsonPropertyName("isBidirectional")] public bool IsBidirectional { get; set; }

    /// <summary>审核状态（0=已生效 1=待审核 2=已驳回）</summary>
    [JsonPropertyName("reviewStatus")] public int ReviewStatus { get; set; }

    /// <summary>审核状态中文标签</summary>
    [JsonPropertyName("reviewStatusLabel")] public string? ReviewStatusLabel { get; set; }

    /// <summary>驳回理由（仅已驳回有值）</summary>
    [JsonPropertyName("reviewComment")] public string? ReviewComment { get; set; }

    /// <summary>提出候选的商户 ID（平台自建为 null）</summary>
    [JsonPropertyName("proposedByMerchantId")] public Guid? ProposedByMerchantId { get; set; }

    /// <summary>提出候选的商户名称（平台自建为 null）</summary>
    [JsonPropertyName("proposedByMerchantName")] public string? ProposedByMerchantName { get; set; }

    /// <summary>数据来源类型（Manual/Crawler/UserSubmitted 等）</summary>
    [JsonPropertyName("dataSourceType")] public string? DataSourceType { get; set; }

    /// <summary>创建时间（UTC，展示层转本地）</summary>
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }

    /// <summary>更新时间（UTC，展示层转本地）</summary>
    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 替代关系中的轴承精简信息，字段对齐 API InterchangeBearingRefDto（camelCase 序列化）
/// </summary>
public class InterchangeBearingRefRowDto
{
    /// <summary>轴承 ID</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }

    /// <summary>型号</summary>
    [JsonPropertyName("partNumber")] public string? PartNumber { get; set; }

    /// <summary>品牌名</summary>
    [JsonPropertyName("brandName")] public string? BrandName { get; set; }

    /// <summary>轴承类型名</summary>
    [JsonPropertyName("bearingType")] public string? BearingType { get; set; }

    /// <summary>内径</summary>
    [JsonPropertyName("innerDiameter")] public decimal InnerDiameter { get; set; }

    /// <summary>外径</summary>
    [JsonPropertyName("outerDiameter")] public decimal OuterDiameter { get; set; }

    /// <summary>宽度</summary>
    [JsonPropertyName("width")] public decimal Width { get; set; }
}