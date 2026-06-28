using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 营业执照审核，调用 API 管理端点
/// </summary>
[Authorize]
public class LicenseController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public LicenseController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    /// <summary>
    /// 待审核营业执照列表（分页）
    /// </summary>
    public async Task<IActionResult> Index(string status = "pending", int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/admin/licenses/pending?page={page}&pageSize={pageSize}";
        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<LicenseItemDto>>>(json, JsonOpts);
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
    /// 通过营业执照审核
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Approve(string id)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{apiBase}/api/admin/licenses/{id}/approve", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "已通过" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 拒绝营业执照审核
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Reject(string id)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{apiBase}/api/admin/licenses/{id}/reject", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "已拒绝" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}
