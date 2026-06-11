using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

public class CrawlerController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public CrawlerController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<IActionResult> Index()
    {
        var baseUrl = _config["ApiUrls:FindBearingsCrawler"] ?? "https://localhost:7207";
        var client = _factory.CreateClient("CrawlerClient");
        try
        {
            var resp = await client.GetAsync($"{baseUrl}/api/crawlers");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<List<CrawlerItemDto>>();
                return View(json ?? []);
            }
        }
        catch { }
        return View(new List<CrawlerItemDto>());
    }

    [HttpPost]
    public async Task<IActionResult> Run(string name)
    {
        var baseUrl = _config["ApiUrls:FindBearingsCrawler"] ?? "https://localhost:7207";
        var client = _factory.CreateClient("CrawlerClient");
        try
        {
            var resp = await client.PostAsync($"{baseUrl}/api/crawlers/{name}/run", null);
            if (resp.IsSuccessStatusCode)
                TempData["Success"] = $"爬虫 {name} 已触发";
            else
                TempData["Error"] = $"触发失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"触发失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}
