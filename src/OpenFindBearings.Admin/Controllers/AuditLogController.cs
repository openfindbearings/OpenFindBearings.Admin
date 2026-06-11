using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

public class AuditLogController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public AuditLogController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 30)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var url = $"{identityBase}/api/account/admin/audit-logs?page={page}&pageSize={pageSize}";
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<List<AuditLogItemDto>>();
                ViewBag.Items = json ?? [];
            }
        }
        catch { }
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }
}
