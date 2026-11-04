using OpenFindBearings.Admin.Authorization;
using OpenFindBearings.Admin.Helpers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 商家牌级治理动作（v2.21.0 牌级整合）：牌级列表已并入商家列表（Data/Merchants），
/// 本控制器只保留人工调级与旧入口兼容跳转；牌级由 API 实时计算。
/// 未认证商户不得授予牌级（API 校验并写日志，权限 merchant.grade.manage）。
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
    /// 商家牌级列表入口（v2.21.0 牌级整合）：独立页已删除，牌级列与调级动作并入商家列表，
    /// 此处保留为兼容跳转——旧书签与纠错页「去调级」链接带过来的参数原样转给 Data/Merchants。
    /// </summary>
    public IActionResult Index(int page = 1, int pageSize = 20, string search = "",
        Guid? merchantId = null, string? merchantName = null, string? reason = null, int? grade = null)
    {
        return RedirectToAction("Merchants", "Data", new
        {
            page,
            pageSize,
            search,
            verifiedOnly = "true",
            merchantId,
            merchantName,
            reason,
            grade
        });
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
