using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.ViewModels;

namespace OpenFindBearings.Admin.Controllers;

[Authorize]
public class ReviewController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(IHttpClientFactory factory, IConfiguration config, ILogger<ReviewController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? type = null, int page = 1)
    {
        var baseUrl = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
        var client = _factory.CreateClient("SyncClient");

        var url = $"{baseUrl}/api/audit/pending?page={page}&pageSize=20";
        if (!string.IsNullOrEmpty(type))
            url += $"&fieldType={Uri.EscapeDataString(type)}";

        try
        {
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("同步数据审核接口返回 {StatusCode}", response.StatusCode);
                ViewBag.Error = "无法获取待审核列表，Sync 服务可能离线";
                return View(new PendingReviewListViewModel { CurrentFilter = type });
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
            {
                ViewBag.Error = "获取待审核列表失败";
                return View(new PendingReviewListViewModel { CurrentFilter = type });
            }

            var vm = new PendingReviewListViewModel { CurrentFilter = type };

            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("items", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        vm.Items.Add(new PendingReviewItemViewModel
                        {
                            Id = item.TryGetProperty("id", out var id) ? id.GetGuid() : Guid.Empty,
                            FieldType = item.TryGetProperty("fieldType", out var ft) ? ft.GetString() ?? "" : "",
                            EntityType = item.TryGetProperty("entityType", out var et) ? et.GetString() ?? "" : "",
                            EntityId = item.TryGetProperty("entityId", out var eid) && eid.ValueKind == JsonValueKind.String ? eid.GetGuid() : null,
                            OriginalValue = item.TryGetProperty("originalValue", out var ov) ? ov.GetString() ?? "" : "",
                            SuggestedValue = item.TryGetProperty("suggestedValue", out var sv) && sv.ValueKind == JsonValueKind.String ? sv.GetString() : null,
                            Confidence = item.TryGetProperty("confidence", out var conf) && conf.ValueKind == JsonValueKind.Number ? conf.GetInt32() : null,
                            CreatedAt = item.TryGetProperty("createdAt", out var ca) && ca.TryGetDateTime(out var dt) ? dt : DateTime.MinValue
                        });
                    }
                }

                if (data.TryGetProperty("totalCount", out var tc))
                    vm.TotalCount = tc.GetInt32();
                if (data.TryGetProperty("pageIndex", out var pi))
                    vm.PageIndex = pi.GetInt32();
                if (data.TryGetProperty("pageSize", out var ps))
                    vm.PageSize = ps.GetInt32();
            }

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取同步数据审核列表异常");
            ViewBag.Error = "连接 Sync 服务异常";
            return View(new PendingReviewListViewModel { CurrentFilter = type });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Approve(Guid id, string? finalValue = null)
    {
        var baseUrl = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
        var client = _factory.CreateClient("SyncClient");

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["approvedBy"] = User.Identity?.Name ?? "admin",
                ["finalValue"] = finalValue ?? ""
            });
            var response = await client.PostAsync($"{baseUrl}/api/audit/{id}/approve", content);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("审核通过: {Id}", id);
                return Json(new { success = true, message = "已通过" });
            }

            _logger.LogWarning("审核通过失败: {Id}, {StatusCode}, {Response}", id, response.StatusCode, json);
            return Json(new { success = false, message = "操作失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "审核通过异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Reject(Guid id, string? reason = null)
    {
        var baseUrl = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
        var client = _factory.CreateClient("SyncClient");

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["rejectedBy"] = User.Identity?.Name ?? "admin",
                ["reason"] = reason ?? "管理员拒绝"
            });
            var response = await client.PostAsync($"{baseUrl}/api/audit/{id}/reject", content);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("审核拒绝: {Id}", id);
                return Json(new { success = true, message = "已拒绝" });
            }

            _logger.LogWarning("审核拒绝失败: {Id}, {StatusCode}, {Response}", id, response.StatusCode, json);
            return Json(new { success = false, message = "操作失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "审核拒绝异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }
}
