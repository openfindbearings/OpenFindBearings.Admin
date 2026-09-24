using OpenFindBearings.Admin.Authorization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;
using Microsoft.Extensions.Logging;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 信息纠错审核，调用 API 纠错端点
/// </summary>
[Authorize]
[PanelPermission("correction.review")]
public class CorrectionController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<CorrectionController> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public CorrectionController(IHttpClientFactory factory, IConfiguration config, ILogger<CorrectionController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 纠错列表，支持按状态筛选（pending/approved/rejected）
    /// </summary>
    public async Task<IActionResult> Index(string status = "pending", int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/admin/corrections?status={status}&page={page}&pageSize={pageSize}";
        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<CorrectionItemDto>>>(json, JsonOpts);
                ViewBag.Items = apiResp?.Data?.Items ?? [];
                ViewBag.TotalCount = apiResp?.Data?.TotalCount ?? 0;
            }
        }
        catch { }
        ViewBag.CurrentStatus = status;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }

    /// <summary>
    /// 采纳纠错（v1.23.0）：代理 API /api/admin/corrections/{id}/approve——
    /// API 侧自动应用建议值到目标实体并标记人工来源（防爬虫覆盖），
    /// 领域事件向提交人发站内信；并发冲突（已被他人处理）409 透传给前端提示刷新
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Approve(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{ApiBase()}/api/admin/corrections/{id}/approve", null);
            var json = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("纠错已采纳: {Id}", id);
                return Json(new { success = true, message = "已采纳，信息已更新并通知提交人" });
            }
            if ((int)resp.StatusCode == 409)
                return Json(new { success = false, conflict = true, message = ExtractProblemDetail(json) ?? "该纠错已被其他管理员处理，请刷新" });

            _logger.LogWarning("纠错采纳失败: {Id}, {StatusCode}, {Response}", id, resp.StatusCode, json);
            return Json(new { success = false, message = ExtractProblemDetail(json) ?? "操作失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "纠错采纳异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }

    /// <summary>
    /// 驳回纠错（v1.23.0）：代理 API reject，驳回原因必填（API 校验），经站内信带给提交人
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Reject(Guid id, string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return Json(new { success = false, message = "请填写驳回原因" });

        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{ApiBase()}/api/admin/corrections/{id}/reject",
                new StringContent(JsonSerializer.Serialize(new { comment }), System.Text.Encoding.UTF8, "application/json"));
            var json = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("纠错已驳回: {Id}", id);
                return Json(new { success = true, message = "已驳回并通知提交人" });
            }
            if ((int)resp.StatusCode == 409)
                return Json(new { success = false, conflict = true, message = ExtractProblemDetail(json) ?? "该纠错已被其他管理员处理，请刷新" });

            _logger.LogWarning("纠错驳回失败: {Id}, {StatusCode}, {Response}", id, resp.StatusCode, json);
            return Json(new { success = false, message = ExtractProblemDetail(json) ?? "操作失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "纠错驳回异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }

    /// <summary>API 基址配置</summary>
    private string ApiBase() => _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";

    /// <summary>从 ProblemDetails JSON 提取 detail 文案（对齐 MerchantVerifyController 同名逻辑）</summary>
    private static string? ExtractProblemDetail(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
        }
        catch { return null; }
    }
}
