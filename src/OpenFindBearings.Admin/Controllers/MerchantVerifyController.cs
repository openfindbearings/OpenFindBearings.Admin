using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 商家认证审核，调用 API 管理端点
/// </summary>
[Authorize]
public class MerchantVerifyController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public MerchantVerifyController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    /// <summary>
    /// 商家列表（分页），用于认证审核
    /// </summary>
    public async Task<IActionResult> Index(string search = "", int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/admin/merchants?page={page}&pageSize={pageSize}";
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
        }
        catch { }
        ViewBag.Search = search;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }

    /// <summary>
    /// 认证商家
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Verify(string id)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{apiBase}/api/admin/merchants/{id}/verify", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "已认证" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}
