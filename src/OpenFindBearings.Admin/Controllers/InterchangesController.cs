using System.Text.Json;
using OpenFindBearings.Admin.Helpers;
using System.Text.Json.Serialization;
using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 替代关系管理控制器（v2.17.0 替代品字典化）：替代关系已成为平台维护的主数据（字典），
/// 由平台人工维护或由数据管线推送后进 API 即生效，不再有"商家报候选 + 平台审核"环节。
/// 管理列表服务端渲染（对齐商品审核页），新建/编辑/删除由浏览器调用
/// Program.cs 注册的代理端点 /api/proxy/admin-interchanges/... 完成。
/// 权限 interchange.view（读取与页面进入）；写操作由 API 侧 interchange.manage 约束。
/// </summary>
[Authorize]
    // 改动说明（v2.17.0 替代品字典化）：替代关系属平台维护的主数据，权限由 merchant.verify 改为 interchange.view
    [PanelPermission("interchange.view")]
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
    /// 替代关系管理列表页：拉取 API /api/admin/interchanges（支持关键字/创建时间/来源过滤），服务端渲染首屏与分页。
    /// API 不可达时降级空表并提示，不阻断页面。
    /// 改动说明（v2.17.0 替代品字典化）：去掉审核状态参数（替代关系不再有审核状态机）
    /// </summary>
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string? keyword = null,
        string? createdFrom = null, string? createdTo = null, string? sourceFilter = null)
    {
        var client = _factory.CreateClient("ApiClient");
        var url = $"{ApiBase()}/api/admin/interchanges?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(keyword))
            url += $"&keyword={Uri.EscapeDataString(keyword)}";
        // 改动说明（v1.40.0 下钻筛选）：透传创建时间区间（业务日历 yyyy-MM-dd）与来源，供仪表盘"替代品·新增"柱下钻
        if (!string.IsNullOrWhiteSpace(createdFrom))
            url += $"&createdFrom={Uri.EscapeDataString(createdFrom)}";
        if (!string.IsNullOrWhiteSpace(createdTo))
            url += $"&createdTo={Uri.EscapeDataString(createdTo)}";
        if (string.Equals(sourceFilter, "internet", StringComparison.OrdinalIgnoreCase))
            url += "&internetSource=true";
        else if (string.Equals(sourceFilter, "noninternet", StringComparison.OrdinalIgnoreCase))
            url += "&internetSource=false";
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
        ViewBag.CreatedFrom = createdFrom;
        ViewBag.CreatedTo = createdTo;
        ViewBag.SourceFilter = sourceFilter;
        return View();
    }
}

/// <summary>
/// 替代关系管理行 DTO，字段对齐 API InterchangeAdminDto（camelCase 序列化）。
/// 改动说明（v2.17.0 替代品字典化）：删除审核状态与提出商户字段（候选流取消，行均为平台维护）
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

    // 改动说明（v2.17.0 替代品字典化）：删除 ReviewStatus/ReviewStatusLabel/ReviewComment/
    //   ProposedByMerchantId/ProposedByMerchantName——商家候选流取消，全部行均为平台维护

    /// <summary>数据来源类型（Manual/Internet/UserSubmitted 等）</summary>
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