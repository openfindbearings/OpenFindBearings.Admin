using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

public class LicenseController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public LicenseController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<IActionResult> Index(string status = "pending", int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/merchants/licenses?status={status}&page={page}&pageSize={pageSize}";
        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<ApiPagedResponse<LicenseItemDto>>();
                ViewBag.Items = json?.Data?.Items ?? [];
                ViewBag.TotalCount = json?.Data?.TotalCount ?? 0;
            }
        }
        catch { }
        ViewBag.CurrentStatus = status;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Approve(string id)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{apiBase}/api/merchants/licenses/{id}/approve", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "已通过" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Reject(string id)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{apiBase}/api/merchants/licenses/{id}/reject", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "已拒绝" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}
