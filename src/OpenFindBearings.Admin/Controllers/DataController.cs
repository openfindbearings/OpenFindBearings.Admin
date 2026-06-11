using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;
using OpenFindBearings.Admin.Models.ViewModels;

namespace OpenFindBearings.Admin.Controllers;

public class DataController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public DataController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<IActionResult> Bearings(string search, int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/bearings/search?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
            return View(new BearingListViewModel());

        var json = await resp.Content.ReadFromJsonAsync<ApiPagedResponse<BearingItemDto>>();
        return View(new BearingListViewModel
        {
            Items = json?.Data?.Items ?? [],
            TotalCount = json?.Data?.TotalCount ?? 0,
            Page = page,
            PageSize = pageSize,
            Search = search
        });
    }

    public async Task<IActionResult> Brands(string search, int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/brands?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
            return View(new BrandListViewModel());

        var json = await resp.Content.ReadFromJsonAsync<ApiPagedResponse<BrandItemDto>>();
        return View(new BrandListViewModel
        {
            Items = json?.Data?.Items ?? [],
            TotalCount = json?.Data?.TotalCount ?? 0,
            Page = page,
            PageSize = pageSize,
            Search = search
        });
    }

    public async Task<IActionResult> BearingTypes(string search, int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/bearing-types?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
            return View(new TypeListViewModel());

        var json = await resp.Content.ReadFromJsonAsync<ApiPagedResponse<BearingTypeItemDto>>();
        return View(new TypeListViewModel
        {
            Items = json?.Data?.Items ?? [],
            TotalCount = json?.Data?.TotalCount ?? 0,
            Page = page,
            PageSize = pageSize,
            Search = search
        });
    }

    public async Task<IActionResult> Merchants(string search, int page = 1, int pageSize = 20)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var url = $"{apiBase}/api/merchants?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
            return View(new MerchantListViewModel());

        var json = await resp.Content.ReadFromJsonAsync<ApiPagedResponse<MerchantItemDto>>();
        return View(new MerchantListViewModel
        {
            Items = json?.Data?.Items ?? [],
            TotalCount = json?.Data?.TotalCount ?? 0,
            Page = page,
            PageSize = pageSize,
            Search = search
        });
    }
}
