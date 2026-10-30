using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Authorization;
using OpenFindBearings.Admin.Helpers;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 意见反馈管理（v1.49.4）：列表分诊 + 一次性回复，调用 API 意见反馈端点
/// </summary>
[Authorize]
[PanelPermission("feedback.view")]
public class FeedbackController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<FeedbackController> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// 构造：注入 HttpClient 工厂、配置与日志
    /// </summary>
    public FeedbackController(IHttpClientFactory factory, IConfiguration config, ILogger<FeedbackController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 反馈列表：支持按处理状态、分类与关键词筛选
    /// </summary>
    public async Task<IActionResult> Index(string status = "", string category = "", string keyword = "", int page = 1, int pageSize = 20)
    {
        var apiBase = ApiUrlResolver.Api(_config);
        var client = _factory.CreateClient("ApiClient");

        var query = $"?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status)) query += $"&status={Uri.EscapeDataString(status)}";
        if (!string.IsNullOrWhiteSpace(category)) query += $"&category={Uri.EscapeDataString(category)}";
        if (!string.IsNullOrWhiteSpace(keyword)) query += $"&keyword={Uri.EscapeDataString(keyword)}";

        try
        {
            var resp = await client.GetAsync($"{apiBase}/api/admin/feedbacks{query}");
            var json = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<FeedbackItemDto>>>(json, JsonOpts);
                ViewBag.Items = apiResp?.Data?.Items ?? [];
                ViewBag.TotalCount = apiResp?.Data?.TotalCount ?? 0;
            }
            else
            {
                // 改动说明（v1.49.4）：上游失败原因透传到页面，避免"永远暂无数据"的误判
                ViewBag.Error = ExtractProblemDetail(json) ?? "加载意见反馈失败";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载意见反馈列表失败");
            ViewBag.Error = "加载意见反馈失败，请稍后重试";
        }

        ViewBag.CurrentStatus = status;
        ViewBag.CurrentCategory = category;
        ViewBag.Keyword = keyword;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }

    /// <summary>
    /// 回复反馈：写入回复并通知提交人（API 侧同时把状态置为已处理）
    /// </summary>
    [HttpPost]
    [PanelPermission("feedback.edit")]
    public async Task<IActionResult> Reply(Guid id, string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return Json(new { success = false, message = "请填写回复内容" });

        var client = _factory.CreateClient("ApiClient");
        try
        {
            // 改动说明（v1.49.4）：API 该端点以复杂类型绑定请求体，空 body 会被判 400，统一发 JSON
            var resp = await client.PostAsync($"{ApiBase()}/api/admin/feedbacks/{id}/reply",
                new StringContent(JsonSerializer.Serialize(new { reply }), System.Text.Encoding.UTF8, "application/json"));
            var json = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("意见反馈已回复: {Id}", id);
                return Json(new { success = true, message = "已回复并通知提交人" });
            }

            _logger.LogWarning("意见反馈回复失败: {Id}, {StatusCode}, {Response}", id, resp.StatusCode, json);
            return Json(new { success = false, message = ExtractProblemDetail(json) ?? "操作失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "意见反馈回复异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }

    /// <summary>
    /// 变更处理状态：仅分诊流转（待处理/处理中/已处理/已关闭），不通知用户
    /// </summary>
    [HttpPost]
    [PanelPermission("feedback.edit")]
    public async Task<IActionResult> UpdateStatus(Guid id, string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return Json(new { success = false, message = "处理状态不正确" });

        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{ApiBase()}/api/admin/feedbacks/{id}/status",
                new StringContent(JsonSerializer.Serialize(new { status }), System.Text.Encoding.UTF8, "application/json"));
            var json = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("意见反馈状态变更: {Id} -> {Status}", id, status);
                return Json(new { success = true, message = "状态已更新" });
            }

            _logger.LogWarning("意见反馈状态变更失败: {Id}, {StatusCode}, {Response}", id, resp.StatusCode, json);
            return Json(new { success = false, message = ExtractProblemDetail(json) ?? "操作失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "意见反馈状态变更异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }

    /// <summary>
    /// 读取 API 地址（统一走 ApiUrlResolver，避免各控制器手拼配置键）
    /// </summary>
    private string ApiBase() => ApiUrlResolver.Api(_config);

    /// <summary>
    /// 从 ProblemDetails 或统一响应体里提取可读错误原因
    /// </summary>
    private static string? ExtractProblemDetail(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString();
            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                return message.GetString();
            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();
        }
        catch (JsonException)
        {
            // 非 JSON 响应（网关错误页等）忽略，由调用方给通用文案
        }

        return null;
    }
}