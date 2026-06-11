using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

public class MerchantVerifyController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public MerchantVerifyController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<IActionResult> Index(string search = "", int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/merchants?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<ApiPagedResponse<MerchantItemDto>>();
                ViewBag.Items = json?.Data?.Items ?? [];
                ViewBag.TotalCount = json?.Data?.TotalCount ?? 0;
            }
        }
        catch { }
        ViewBag.Search = search;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Verify(string id)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{apiBase}/api/merchants/{id}/verify", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "已认证" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}
