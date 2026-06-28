using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 映射维护，展示品牌/类型的 API 数据（只读）
/// </summary>
[Authorize]
public class MappingController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public MappingController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    /// <summary>
    /// 品牌或类型列表（非分页，返回全部）
    /// </summary>
    public async Task<IActionResult> Index(string type = "brand")
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var url = type == "brand"
                ? $"{apiBase}/api/brands"
                : $"{apiBase}/api/bearing-types";
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                // 品牌和类型 API 返回非分页数组：{ success, data: [...] }
                var apiResp = JsonSerializer.Deserialize<ApiResponse<List<BrandItemDto>>>(json, JsonOpts);
                ViewBag.Items = apiResp?.Data ?? [];
                ViewBag.TotalCount = apiResp?.Data?.Count ?? 0;
            }
        }
        catch { }
        ViewBag.MappingType = type;
        return View();
    }
}
