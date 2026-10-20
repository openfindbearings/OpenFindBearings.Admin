using OpenFindBearings.Admin.Authorization;
using OpenFindBearings.Admin.Helpers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 寻货治理（v1.35.0）：全状态需求列表 + 详情抽屉（需求全文/应答明细/发布人联系方式）+ 违规下架。
/// 先发后审模式：发布即上线，本页承担事后治理（下架软删并站内信通知发布人）。
/// 数据全部代理 OpenFindBearings.Api 的 /api/admin/sourcing/*（Admin 不直连业务库）
/// </summary>
[Authorize]
public class SourcingController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SourcingController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    /// <summary>
    /// 寻货列表：状态筛选 + 型号关键词 + 分页（sourcing.view）
    /// </summary>
    [PanelPermission("sourcing.view")]
    public async Task<IActionResult> Index(int? status = null, string search = "", int page = 1, int pageSize = 20)
    {
        var apiBase = ApiUrlResolver.Api(_config);
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/admin/sourcing/demands?page={page}&pageSize={pageSize}";
        if (status.HasValue) url += $"&status={status.Value}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&keyword={Uri.EscapeDataString(search)}";
        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonSerializer.Deserialize<ApiResponse<SourcingListData>>(json, JsonOpts);
                ViewBag.Items = apiResp?.Data?.Items ?? [];
                ViewBag.TotalCount = apiResp?.Data?.Total ?? 0;
            }
        }
        catch { }
        ViewBag.Status = status;
        ViewBag.Search = search;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }

    /// <summary>
    /// 寻货详情 JSON（抽屉数据源：需求全文+全部应答+发布人联系方式）
    /// </summary>
    [PanelPermission("sourcing.view")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var apiBase = ApiUrlResolver.Api(_config);
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.GetAsync($"{apiBase}/api/admin/sourcing/demands/{id}");
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return Json(new { success = false, message = "加载失败" });
            var apiResp = JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);
            return Json(new { success = true, data = apiResp.GetProperty("data") });
        }
        catch
        {
            return Json(new { success = false, message = "服务异常" });
        }
    }

    /// <summary>
    /// 下架寻货（违规治理，sourcing.manage；下架原因透传发布人站内信）
    /// </summary>
    [HttpPost]
    [PanelPermission("sourcing.manage")]
    public async Task<IActionResult> TakeDown(Guid id, string? reason)
    {
        var apiBase = ApiUrlResolver.Api(_config);
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsJsonAsync($"{apiBase}/api/admin/sourcing/demands/{id}/takedown", new { reason });
            if (resp.IsSuccessStatusCode)
                return Json(new { success = true, message = "已下架并通知发布人" });
            var json = await resp.Content.ReadAsStringAsync();
            return Json(new { success = false, message = $"下架失败（{(int)resp.StatusCode}）" });
        }
        catch
        {
            return Json(new { success = false, message = "服务异常" });
        }
    }

    /// <summary>
    /// 成交回执记录（v2.20.0 管理侧复核）：分页查看需求方回执，可按商户与结果筛选，
    /// 含是否计分、未计分原因与复核留痕（sourcing.view）
    /// </summary>
    [PanelPermission("sourcing.view")]
    public async Task<IActionResult> Feedbacks(Guid? merchantId, int? outcome, int page = 1, int pageSize = 20)
    {
        var apiBase = ApiUrlResolver.Api(_config);
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/admin/sourcing/feedbacks?page={page}&pageSize={pageSize}";
        if (merchantId.HasValue)
            url += $"&merchantId={merchantId.Value}";
        if (outcome.HasValue)
            url += $"&outcome={outcome.Value}";
        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonSerializer.Deserialize<ApiResponse<SourcingFeedbackListData>>(json, JsonOpts);
                ViewBag.Items = apiResp?.Data?.Items ?? [];
                ViewBag.Total = apiResp?.Data?.Total ?? 0;
            }
        }
        catch { }
        ViewBag.MerchantId = merchantId;
        ViewBag.Outcome = outcome;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }

    /// <summary>
    /// 撤销或恢复成交回执计分（merchant.grade.manage）：counted=false 撤销计分，counted=true 恢复计分，
    /// API 会同步重算商家卡级并记录复核原因
    /// </summary>
    [HttpPost]
    [PanelPermission("merchant.grade.manage")]
    public async Task<IActionResult> RevokeFeedback(Guid responseId, bool counted, string? reason)
    {
        var apiBase = ApiUrlResolver.Api(_config);
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsJsonAsync(
                $"{apiBase}/api/admin/sourcing/feedbacks/{responseId}/revoke",
                new { counted, reason });
            var json = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                var message = ExtractApiMessage(json) ?? (counted ? "已恢复计分" : "已撤销计分");
                return Json(new { success = true, message });
            }
            return Json(new { success = false, message = ExtractApiMessage(json) ?? "操作失败" });
        }
        catch
        {
            return Json(new { success = false, message = "服务异常" });
        }
    }

    /// <summary>
    /// 从 API 响应中提取可读文案：优先 message（ApiResponse），其次 detail（ProblemDetails）
    /// </summary>
    private static string? ExtractApiMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var message)
                && !string.IsNullOrWhiteSpace(message.GetString()))
                return message.GetString();
            if (doc.RootElement.TryGetProperty("detail", out var detail)
                && !string.IsNullOrWhiteSpace(detail.GetString()))
                return detail.GetString();
        }
        catch { }
        return null;
    }
}

/// <summary>Admin 寻货列表行（对齐 API /api/admin/sourcing/demands items）</summary>
public class SourcingAdminItemDto
{
    public Guid Id { get; set; }
    public string PartNumber { get; set; } = "";
    public string? Brand { get; set; }
    public string? Quantity { get; set; }
    public string? Region { get; set; }
    public int Status { get; set; }
    public int ResponseCount { get; set; }
    public string? PublisherName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiryAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    /// <summary>改动说明（v1.44.0 公开/邀请）：曝光范围——true=邀请（不进公开大厅，仅收件商家可见）</summary>
    public bool IsPrivate { get; set; }
    /// <summary>邀请收件人家数（公开单恒 0；治理视图用于判断触达范围）</summary>
    public int InvitedCount { get; set; }
    /// <summary>改动说明（v1.44.0）：发布方身份——个人名义 / 商户名义（治理时需知道责任主体）</summary>
    public string? PublisherType { get; set; }
    /// <summary>发布商户名称（商户名义发布时的责任主体；个人名义为空）</summary>
    public string? PublisherMerchantName { get; set; }
    /// <summary>改动说明（v1.45.0 治理可见性）：软删标记——治理列表纳入发布人已删单，需在列表中标出，避免误判为在库单</summary>
    public bool IsDeleted { get; set; }
}

/// <summary>列表数据包（API 匿名对象 {items,total} 对齐）</summary>
public class SourcingListData
{
    public List<SourcingAdminItemDto> Items { get; set; } = [];
    public int Total { get; set; }
}

/// <summary>Admin 成交回执行（对齐 API /api/admin/sourcing/feedbacks items）</summary>
public class SourcingFeedbackDto
{
    /// <summary>应答记录 ID（撤销/恢复计分的操作键）</summary>
    public Guid ResponseId { get; set; }
    /// <summary>所属寻货需求 ID</summary>
    public Guid DemandId { get; set; }
    /// <summary>轴承型号（需求型号）</summary>
    public string? PartNumber { get; set; }
    /// <summary>应答商家 ID</summary>
    public Guid MerchantId { get; set; }
    /// <summary>应答商家名称</summary>
    public string? MerchantName { get; set; }
    /// <summary>需求发布人昵称</summary>
    public string? PublisherName { get; set; }
    /// <summary>需求发布商户名称（商户名义发布时有值）</summary>
    public string? PublisherMerchantName { get; set; }
    /// <summary>回执结果数值（0 谈成 / 1 没货 / 2 加价 / 3 拖延 / 4 其他）</summary>
    public int Outcome { get; set; }
    /// <summary>回执结果中文标签（API 下发口径）</summary>
    public string? OutcomeLabel { get; set; }
    /// <summary>需求方填写的回执原因</summary>
    public string? Reason { get; set; }
    /// <summary>是否计入商家卡级成功撮合次数</summary>
    public bool Counted { get; set; }
    /// <summary>未计分复核留痕（反刷校验或管理侧复核说明）</summary>
    public string? FlagNote { get; set; }
    /// <summary>回执关联的选定时间</summary>
    public DateTime? AdoptedAt { get; set; }
    /// <summary>回执提交时间</summary>
    public DateTime? FeedbackAt { get; set; }
}

/// <summary>成交回执列表数据包（API 匿名对象 {items,total} 对齐）</summary>
public class SourcingFeedbackListData
{
    public List<SourcingFeedbackDto> Items { get; set; } = [];
    public int Total { get; set; }
}
