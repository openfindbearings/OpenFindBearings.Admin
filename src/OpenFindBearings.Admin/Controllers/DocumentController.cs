using OpenFindBearings.Admin.Authorization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 商户证照材料审核（v2.7.0 由"营业执照审核"LicenseController 泛化改名）。
/// 只承载入驻后的材料变更队列（换证/补授权书/厂房照）；
/// 随入驻申请提交的材料在"入驻申请审批"抽屉内级联审，不进本队列。
/// </summary>
[Authorize]
[PanelPermission("merchant.verify")]
public class DocumentController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public DocumentController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    /// <summary>
    /// 待审核证照材料列表（分页）
    /// </summary>
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/admin/documents/pending?page={page}&pageSize={pageSize}";
        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<DocumentItemDto>>>(json, JsonOpts);
                ViewBag.Items = apiResp?.Data?.Items ?? [];
                ViewBag.TotalCount = apiResp?.Data?.TotalCount ?? 0;
            }
        }
        catch { }
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }

    /// <summary>
    /// 通过单条材料审核（不自动认证——认证由"入驻申请审批"页 verify 按材料矩阵口径判定）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Approve(string id)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{apiBase}/api/admin/documents/{id}/approve", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "材料已通过" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 拒绝单条材料审核（携带理由回传商户端）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Reject(string id, string? reason)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            // 改动说明：旧实现拒绝不带原因（API RejectDocumentCommand.Reason 恒空），
            //   现随表单提交理由，商户端可见具体驳回原因
            var content = new StringContent(
                JsonSerializer.Serialize(new { reason = string.IsNullOrWhiteSpace(reason) ? "材料未通过审核" : reason.Trim() }),
                System.Text.Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{apiBase}/api/admin/documents/{id}/reject", content);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "已拒绝" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}
