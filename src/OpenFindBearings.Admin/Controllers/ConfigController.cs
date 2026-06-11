using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

public class ConfigController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public ConfigController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<IActionResult> Index()
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.GetAsync($"{apiBase}/api/config");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<List<SystemConfigDto>>();
                ViewBag.Items = json ?? [];
            }
        }
        catch { }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Update(string key, string value)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PutAsJsonAsync($"{apiBase}/api/config/{key}", new { value });
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "配置已更新" : $"更新失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"更新失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}
