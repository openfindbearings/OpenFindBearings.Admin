using OpenFindBearings.Admin.Authorization;
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
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
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
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
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
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
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
}

/// <summary>列表数据包（API 匿名对象 {items,total} 对齐）</summary>
public class SourcingListData
{
    public List<SourcingAdminItemDto> Items { get; set; } = [];
    public int Total { get; set; }
}
