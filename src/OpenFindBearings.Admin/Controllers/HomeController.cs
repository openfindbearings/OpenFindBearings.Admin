using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models;
using OpenFindBearings.Admin.Services;
using System.Diagnostics;

namespace OpenFindBearings.Admin.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ServiceHealthService _health;
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public HomeController(ServiceHealthService health, IHttpClientFactory factory, IConfiguration config)
    {
        _health = health;
        _factory = factory;
        _config = config;
    }

    public IActionResult Index()
    {
        return View();
    }

    [AllowAnonymous]
    public async Task<IActionResult> Status()
    {
        var result = await _health.CheckAllAsync();
        return Json(result);
    }

    [AllowAnonymous]
    public async Task<IActionResult> DashboardStats()
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        client.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            var resp = await client.GetAsync($"{apiBase}/api/admin/dashboard/stats");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }
        }
        catch { }

        return Json(new
        {
            bearingCount = "N/A",
            brandCount = "N/A",
            merchantCount = "N/A"
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
