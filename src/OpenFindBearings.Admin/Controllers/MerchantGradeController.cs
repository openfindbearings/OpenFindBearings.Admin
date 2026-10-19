using OpenFindBearings.Admin.Authorization;
using OpenFindBearings.Admin.Helpers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 商家卡级管理（v2.20.0）：查看商家当前卡级并人工调整。
/// 卡级规则由 API 实时计算，本页只承担申诉核实后的补偿与异常刷分核实后的降级；
/// 未认证商户不得授予卡级，调整会写入 API 审计日志（merchant.grade.manage）。
/// 数据全部代理 OpenFindBearings.Api 的 /api/admin/merchants（Admin 不直连业务库）
/// </summary>
[Authorize]
[PanelPermission("merchant.grade.manage")]
public class MerchantGradeController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>构造：注入 HTTP 工厂与配置</summary>
    public MerchantGradeController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    /// <summary>
    /// 商家卡级列表：名称/公司/认证状态/当前卡级 + 搜索与分页（merchant.grade.manage）
    /// </summary>
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string search = "")
    {
        var apiBase = ApiUrlResolver.Api(_config);
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/admin/merchants?page={page}&pageSize={pageSize}&includeDeleted=false";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&keyword={Uri.EscapeDataString(search)}";
        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<MerchantItemDto>>>(json, JsonOpts);
                ViewBag.Items = apiResp?.Data?.Items ?? [];
                ViewBag.TotalCount = apiResp?.Data?.TotalCount ?? 0;
            }
            else
            {
                ViewBag.Error = "无法获取商家列表";
            }
        }
        catch
        {
            ViewBag.Error = "连接 API 服务异常";
        }
        ViewBag.Search = search;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }

    /// <summary>
    /// 人工调整商家卡级（merchant.grade.manage）：grade 取 1 注册商户 / 2 铜卡 / 3 银卡 / 4 金卡，
    /// API 对未认证商户授予高卡级返回 400，文案透传给前端提示
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Adjust(Guid id, int grade, string? reason)
    {
        var apiBase = ApiUrlResolver.Api(_config);
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsJsonAsync(
                $"{apiBase}/api/admin/merchants/{id}/grade",
                new { grade, reason });
            var json = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                var message = ExtractApiMessage(json) ?? "卡级已调整";
                return Json(new { success = true, message });
            }
            return Json(new { success = false, message = ExtractApiMessage(json) ?? "调整失败" });
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
