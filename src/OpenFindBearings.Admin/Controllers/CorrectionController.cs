using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

public class CorrectionController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public CorrectionController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<IActionResult> Index(string status = "pending", int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/corrections?status={status}&page={page}&pageSize={pageSize}";
        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<ApiPagedResponse<CorrectionItemDto>>();
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
}
