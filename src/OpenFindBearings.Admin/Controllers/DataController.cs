using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;
using OpenFindBearings.Admin.Models.ViewModels;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 数据管理控制器
/// </summary>
[Authorize]
public class DataController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<DataController> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public DataController(IHttpClientFactory factory, IConfiguration config, ILogger<DataController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    private string ApiBase() => _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";

    #region 轴承

    /// <summary>
    /// 轴承列表（分页），支持显示已删除项
    /// </summary>
    public async Task<IActionResult> Bearings(string? search, bool includeDeleted = false, int page = 1, int pageSize = 20)
    {
        var client = _factory.CreateClient("ApiClient");
        var url = $"{ApiBase()}/api/admin/bearings?page={page}&pageSize={pageSize}&includeDeleted={includeDeleted.ToString().ToLower()}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&keyword={Uri.EscapeDataString(search)}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync();
            _logger.LogWarning("API 请求失败: {Status} {Url} {Body}", resp.StatusCode, url, errBody);
            return View(new BearingListViewModel());
        }

        var json = await resp.Content.ReadAsStringAsync();
        var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<BearingItemDto>>>(json, JsonOpts);
        return View(new BearingListViewModel
        {
            Items = apiResp?.Data?.Items ?? [],
            TotalCount = apiResp?.Data?.TotalCount ?? 0,
            Page = page,
            PageSize = pageSize,
            Search = search,
            IncludeDeleted = includeDeleted
        });
    }

    /// <summary>
    /// 创建轴承，POST /api/admin/bearings
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateBearing(string partNumber, string? oldNumber, string? description, string? brandName, string? bearingType)
    {
        var client = _factory.CreateClient("ApiClient");
        var body = new { partNumber, oldNumber, description, brandName, bearingType };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PostAsync($"{ApiBase()}/api/admin/bearings", content);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "轴承已创建" : $"创建失败: {resp.StatusCode}";
        return RedirectToAction("Bearings");
    }

    /// <summary>
    /// 编辑轴承，PUT /api/admin/bearings/{id}
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EditBearing(Guid id, string? partNumber, string? oldNumber, string? description, string? brandName, string? bearingType)
    {
        var client = _factory.CreateClient("ApiClient");
        var body = new { partNumber, oldNumber, description, brandName, bearingType };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PutAsync($"{ApiBase()}/api/admin/bearings/{id}", content);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "轴承已更新" : $"更新失败: {resp.StatusCode}";
        return RedirectToAction("Bearings");
    }

    /// <summary>
    /// 删除轴承（软删除），DELETE /api/admin/bearings/{id}
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteBearing(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.DeleteAsync($"{ApiBase()}/api/admin/bearings/{id}");
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "轴承已删除" : $"删除失败: {resp.StatusCode}";
        return RedirectToAction("Bearings");
    }

    /// <summary>
    /// 恢复已删除轴承，PUT /api/admin/bearings/{id}/restore
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RestoreBearing(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.PutAsync($"{ApiBase()}/api/admin/bearings/{id}/restore", null);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "轴承已恢复" : $"恢复失败: {resp.StatusCode}";
        return RedirectToAction("Bearings");
    }

    /// <summary>
    /// 彻底删除轴承，DELETE /api/admin/bearings/{id}/hard
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HardDeleteBearing(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.DeleteAsync($"{ApiBase()}/api/admin/bearings/{id}/hard");
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "轴承已彻底删除" : $"彻底删除失败: {resp.StatusCode}";
        return RedirectToAction("Bearings");
    }

    #endregion

    #region 品牌

    /// <summary>
    /// 品牌列表，API 返回 { success, data: [brand1, ...] }（非分页）
    /// </summary>
    public async Task<IActionResult> Brands(string? search, bool includeDeleted = false, int page = 1, int pageSize = 20)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.GetAsync($"{ApiBase()}/api/admin/brands?includeDeleted={includeDeleted.ToString().ToLower()}");
        if (!resp.IsSuccessStatusCode)
            return View(new BrandListViewModel());

        var json = await resp.Content.ReadAsStringAsync();
        var apiResp = JsonSerializer.Deserialize<ApiResponse<List<BrandItemDto>>>(json, JsonOpts);
        var allItems = apiResp?.Data ?? [];
        if (!string.IsNullOrWhiteSpace(search))
            allItems = allItems.Where(b => b.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || b.Code.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        var pagedItems = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return View(new BrandListViewModel
        {
            Items = pagedItems,
            TotalCount = allItems.Count,
            Page = page,
            PageSize = pageSize,
            Search = search,
            IncludeDeleted = includeDeleted
        });
    }

    /// <summary>
    /// 创建品牌，POST /api/admin/brands
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateBrand(string code, string name, string? country, string? logoUrl, string? level)
    {
        var client = _factory.CreateClient("ApiClient");
        var body = new { code, name, country, logoUrl, level };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PostAsync($"{ApiBase()}/api/admin/brands", content);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "品牌已创建" : $"创建失败: {resp.StatusCode}";
        return RedirectToAction("Brands");
    }

    /// <summary>
    /// 编辑品牌，PUT /api/admin/brands/{id}
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EditBrand(Guid id, string name, string? country, string? logoUrl, string? level)
    {
        var client = _factory.CreateClient("ApiClient");
        var body = new { name, country, logoUrl, level };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PutAsync($"{ApiBase()}/api/admin/brands/{id}", content);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "品牌已更新" : $"更新失败: {resp.StatusCode}";
        return RedirectToAction("Brands");
    }

    /// <summary>
    /// 删除品牌（软删除），DELETE /api/admin/brands/{id}
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteBrand(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.DeleteAsync($"{ApiBase()}/api/admin/brands/{id}");
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "品牌已删除" : $"删除失败: {resp.StatusCode}";
        return RedirectToAction("Brands");
    }

    /// <summary>
    /// 恢复已删除品牌，PUT /api/admin/brands/{id}/restore
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RestoreBrand(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.PutAsync($"{ApiBase()}/api/admin/brands/{id}/restore", null);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "品牌已恢复" : $"恢复失败: {resp.StatusCode}";
        return RedirectToAction("Brands");
    }

    /// <summary>
    /// 彻底删除品牌，DELETE /api/admin/brands/{id}/hard
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HardDeleteBrand(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.DeleteAsync($"{ApiBase()}/api/admin/brands/{id}/hard");
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "品牌已彻底删除" : $"彻底删除失败: {resp.StatusCode}";
        return RedirectToAction("Brands");
    }

    #endregion

    #region 类型

    /// <summary>
    /// 类型列表，API 返回 { success, data: [type1, ...] }（非分页）
    /// </summary>
    public async Task<IActionResult> BearingTypes(string? search, bool includeDeleted = false, int page = 1, int pageSize = 20)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.GetAsync($"{ApiBase()}/api/admin/bearing-types?includeDeleted={includeDeleted.ToString().ToLower()}");
        if (!resp.IsSuccessStatusCode)
            return View(new TypeListViewModel());

        var json = await resp.Content.ReadAsStringAsync();
        var apiResp = JsonSerializer.Deserialize<ApiResponse<List<BearingTypeItemDto>>>(json, JsonOpts);
        var allItems = apiResp?.Data ?? [];
        if (!string.IsNullOrWhiteSpace(search))
            allItems = allItems.Where(t => t.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || t.Code.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        var pagedItems = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return View(new TypeListViewModel
        {
            Items = pagedItems,
            TotalCount = allItems.Count,
            Page = page,
            PageSize = pageSize,
            Search = search,
            IncludeDeleted = includeDeleted
        });
    }

    /// <summary>
    /// 创建类型，POST /api/admin/bearing-types
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateBearingType(string code, string name, string? description)
    {
        var client = _factory.CreateClient("ApiClient");
        var body = new { code, name, description };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PostAsync($"{ApiBase()}/api/admin/bearing-types", content);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "类型已创建" : $"创建失败: {resp.StatusCode}";
        return RedirectToAction("BearingTypes");
    }

    /// <summary>
    /// 编辑类型，PUT /api/admin/bearing-types/{id}
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EditBearingType(Guid id, string name, string? description)
    {
        var client = _factory.CreateClient("ApiClient");
        var body = new { name, description };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PutAsync($"{ApiBase()}/api/admin/bearing-types/{id}", content);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "类型已更新" : $"更新失败: {resp.StatusCode}";
        return RedirectToAction("BearingTypes");
    }

    /// <summary>
    /// 删除类型（软删除），DELETE /api/admin/bearing-types/{id}
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteBearingType(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.DeleteAsync($"{ApiBase()}/api/admin/bearing-types/{id}");
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "类型已删除" : $"删除失败: {resp.StatusCode}";
        return RedirectToAction("BearingTypes");
    }

    /// <summary>
    /// 恢复已删除类型，PUT /api/admin/bearing-types/{id}/restore
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RestoreBearingType(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.PutAsync($"{ApiBase()}/api/admin/bearing-types/{id}/restore", null);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "类型已恢复" : $"恢复失败: {resp.StatusCode}";
        return RedirectToAction("BearingTypes");
    }

    /// <summary>
    /// 彻底删除类型，DELETE /api/admin/bearing-types/{id}/hard
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HardDeleteBearingType(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.DeleteAsync($"{ApiBase()}/api/admin/bearing-types/{id}/hard");
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "类型已彻底删除" : $"彻底删除失败: {resp.StatusCode}";
        return RedirectToAction("BearingTypes");
    }

    #endregion

    #region 商家

    /// <summary>
    /// 商家列表（分页），支持显示已删除项
    /// </summary>
    public async Task<IActionResult> Merchants(string? search, string? verifiedOnly, bool includeDeleted = false, int page = 1, int pageSize = 20)
    {
        var client = _factory.CreateClient("ApiClient");
        var url = $"{ApiBase()}/api/admin/merchants?page={page}&pageSize={pageSize}&includeDeleted={includeDeleted.ToString().ToLower()}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&keyword={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(verifiedOnly))
            url += $"&verifiedOnly={verifiedOnly}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
            return View(new MerchantListViewModel());

        var json = await resp.Content.ReadAsStringAsync();
        var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<MerchantItemDto>>>(json, JsonOpts);
        return View(new MerchantListViewModel
        {
            Items = apiResp?.Data?.Items ?? [],
            TotalCount = apiResp?.Data?.TotalCount ?? 0,
            Page = page,
            PageSize = pageSize,
            Search = search,
            VerifiedOnly = verifiedOnly,
            IncludeDeleted = includeDeleted
        });
    }

    /// <summary>
    /// 创建商家，POST /api/admin/merchants
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateMerchant(string name, string? companyName, string? englishName, string? contactPerson, string? phone, string? email, string? address)
    {
        var client = _factory.CreateClient("ApiClient");
        var body = new { name, companyName, englishName, contactPerson, phone, email, address, type = 4 };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PostAsync($"{ApiBase()}/api/admin/merchants", content);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "商家已创建" : $"创建失败: {resp.StatusCode}";
        return RedirectToAction("Merchants");
    }

    /// <summary>
    /// 编辑商家，PUT /api/admin/merchants/{id}
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EditMerchant(Guid id, string? name, string? companyName, string? englishName, string? contactPerson, string? phone, string? email, string? address)
    {
        var client = _factory.CreateClient("ApiClient");
        var body = new { name, companyName, englishName, contactPerson, phone, email, address };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PutAsync($"{ApiBase()}/api/admin/merchants/{id}", content);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "商家已更新" : $"更新失败: {resp.StatusCode}";
        return RedirectToAction("Merchants");
    }

    /// <summary>
    /// 删除商家（软删除），DELETE /api/admin/merchants/{id}
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteMerchant(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.DeleteAsync($"{ApiBase()}/api/admin/merchants/{id}");
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "商家已删除" : $"删除失败: {resp.StatusCode}";
        return RedirectToAction("Merchants");
    }

    /// <summary>
    /// 恢复已删除商家，PUT /api/admin/merchants/{id}/restore
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RestoreMerchant(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.PutAsync($"{ApiBase()}/api/admin/merchants/{id}/restore", null);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "商家已恢复" : $"恢复失败: {resp.StatusCode}";
        return RedirectToAction("Merchants");
    }

    /// <summary>
    /// 彻底删除商家，DELETE /api/admin/merchants/{id}/hard
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HardDeleteMerchant(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.DeleteAsync($"{ApiBase()}/api/admin/merchants/{id}/hard");
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "商家已彻底删除" : $"彻底删除失败: {resp.StatusCode}";
        return RedirectToAction("Merchants");
    }

    [HttpGet]
    public async Task<IActionResult> GetMerchantBearings(Guid id, bool onlyOnSale = true, int page = 1, int pageSize = 20)
    {
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var url = $"{ApiBase()}/api/merchants/{id}/bearings?page={page}&pageSize={pageSize}";
            if (onlyOnSale) url += "&onlyOnSale=true";

            var resp = await client.GetAsync(url);
            var json = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
                return Content(json, "application/json");

            _logger.LogWarning("获取商家在售商品失败: {Id}, {StatusCode}", id, resp.StatusCode);
            return Json(new { success = false, message = "获取失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取商家在售商品异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }

    #endregion

    #region 库存导入

    /// <summary>
    /// 库存导入页面
    /// </summary>
    public async Task<IActionResult> ImportInventory(Guid? selectedMerchantId = null)
    {
        var apiClient = _factory.CreateClient("ApiClient");
        var apiBase = ApiBase();
        var syncBase = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";

        // 获取商家列表（下拉选择用）
        ViewBag.Merchants = Array.Empty<MerchantItemDto>();
        try
        {
            var resp = await apiClient.GetAsync($"{apiBase}/api/admin/merchants?page=1&pageSize=1000&includeDeleted=false");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<MerchantItemDto>>>(json, JsonOpts);
                ViewBag.Merchants = apiResp?.Data?.Items ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取商家列表失败");
        }

        ViewBag.SelectedMerchantId = selectedMerchantId;
        ViewBag.SyncBase = syncBase;
        return View();
    }

    /// <summary>
    /// 下载库存导入模板（代理 Sync API）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DownloadInventoryTemplate()
    {
        var syncClient = _factory.CreateClient("SyncClient");
        var syncBase = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";

        try
        {
            var resp = await syncClient.GetAsync($"{syncBase}/api/inventory/template");
            if (!resp.IsSuccessStatusCode)
                return NotFound("模板文件不可用");

            var stream = await resp.Content.ReadAsStreamAsync();
            return File(stream, resp.Content.Headers.ContentType?.MediaType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "库存导入模板.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载库存导入模板失败");
            return NotFound("模板文件不可用");
        }
    }

    /// <summary>
    /// 提交库存导入
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ImportInventory(Guid merchantId, IFormFile? file)
    {
        var syncClient = _factory.CreateClient("SyncClient");
        var syncBase = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "请选择要上传的 Excel 文件";
            return RedirectToAction("ImportInventory", new { selectedMerchantId = merchantId });
        }

        try
        {
            using var formData = new MultipartFormDataContent();
            formData.Add(new StreamContent(file.OpenReadStream()), "file", file.FileName);

            var resp = await syncClient.PostAsync($"{syncBase}/api/inventory/import?merchantId={merchantId}", formData);

            var json = await resp.Content.ReadAsStringAsync();
            var apiResp = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(json, JsonOpts);

            if (resp.IsSuccessStatusCode && apiResp?.Success == true)
            {
                var data = apiResp.Data;
                if (data.ValueKind == JsonValueKind.Object)
                {
                    var total = data.GetProperty("totalRows").GetInt32();
                    var succeeded = data.GetProperty("succeeded").GetInt32();
                    var failed = data.GetProperty("failed").GetInt32();
                    TempData["Success"] = $"导入完成: 共 {total} 条, 成功 {succeeded} 条, 失败 {failed} 条";
                }
                else
                {
                    TempData["Success"] = apiResp.Message ?? "导入完成";
                }
            }
            else
            {
                TempData["Error"] = $"导入失败: {apiResp?.Message ?? json}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "库存导入异常");
            TempData["Error"] = $"导入异常: {ex.Message}";
        }

        return RedirectToAction("ImportInventory", new { selectedMerchantId = merchantId });
    }

    #endregion
}
