using OpenFindBearings.Admin.Authorization;
using OpenFindBearings.Admin.Helpers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 商家牌级管理（v1.48.1，v1.48.0 有效期制适配）：查看商家当前牌级与人工调整；
/// 牌级由 API 实时计算，本页只做治理动作和异常刷新后的结果核对。
/// 未认证商户不得授予牌级（API 校验并写日志，权限 merchant.grade.manage）。
/// 数据全部来自 OpenFindBearings.Api 的 /api/admin/merchants（Admin 不直连业务库）
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
    /// 商家牌级列表：名称/公司/认证状态/当前牌级 + 有效期 + 调级入口（merchant.grade.manage）。
    /// 改动说明（v1.47.1 卡级申诉联动）：支持从纠错审核页带 merchantId/merchantName/reason/grade 跳入，
    /// 页面顶部提示申诉来源并预填调级弹窗——采纳纠错本身不改级，必须在此二次确认（权责分离）
    /// 改动说明（v1.48.0 有效期制）：列表新增有效期止与在售保级状态
    /// </summary>
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string search = "",
        Guid? merchantId = null, string? merchantName = null, string? reason = null, int? grade = null)
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
        // 改动说明（v1.47.1）：申诉来源透传到视图，用于顶部提示与调级弹窗预填
        ViewBag.AppealMerchantId = merchantId;
        ViewBag.AppealMerchantName = merchantName;
        ViewBag.AppealReason = reason;
        ViewBag.AppealGrade = grade;
        return View();
    }

    /// <summary>
    /// 人工调整商家牌级（merchant.grade.manage）：grade 取 1 普通 / 2 铜牌 / 3 银牌 / 4 金牌，
    /// API 对未认证商户授予高牌级返回 400，文案透传给前端提示；调级成功会重起一个有效期
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
