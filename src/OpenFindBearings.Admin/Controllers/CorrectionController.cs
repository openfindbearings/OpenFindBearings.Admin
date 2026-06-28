using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 信息纠错审核，调用 API 纠错端点
/// </summary>
[Authorize]
public class CorrectionController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public CorrectionController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
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
}
