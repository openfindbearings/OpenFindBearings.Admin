using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

public class UsersController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public UsersController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<IActionResult> Index(string search = "", int page = 1, int pageSize = 20)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        var url = $"{identityBase}/api/account/admin/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<List<UserItemDto>>();
                ViewBag.Items = json ?? [];
            }
        }
        catch { }
        ViewBag.Search = search;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var resp = await client.PostAsync($"{identityBase}/api/account/admin/users/{id}/toggle-status", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "状态已切换" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}
