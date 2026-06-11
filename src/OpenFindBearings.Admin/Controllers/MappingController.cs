using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

public class MappingController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public MappingController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

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
                var json = await resp.Content.ReadFromJsonAsync<ApiPagedResponse<object>>();
                ViewBag.Items = json?.Data?.Items ?? [];
                ViewBag.TotalCount = json?.Data?.TotalCount ?? 0;
            }
        }
        catch { }
        ViewBag.MappingType = type;
        return View();
    }
}
