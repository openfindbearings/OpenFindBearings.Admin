using OpenFindBearings.Admin.Authorization;
using OpenFindBearings.Admin.Helpers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;
using OpenFindBearings.Admin.Models.ViewModels;
using OpenFindBearings.Admin.Services;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 数据管理控制器
/// </summary>
[Authorize]
[PanelPermission("bearing.view", "brand.view", "type.view", "merchant.view")]
public class DataController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<DataController> _logger;
    private readonly PriceConfigService _priceConfig;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public DataController(
        IHttpClientFactory factory,
        IConfiguration config,
        ILogger<DataController> logger,
        PriceConfigService priceConfig)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
        _priceConfig = priceConfig;
    }

    private string ApiBase() => ApiUrlResolver.Api(_config);

    /// <summary>
    /// 读取上游错误正文中的可读原因（detail/title/message），取不到时回退状态码
    /// 改动说明（v1.49.1）：新建/线索操作失败时只显示 400/500 无法定位原因，需把校验消息透给管理员
    /// </summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                foreach (var key in new[] { "detail", "title", "message" })
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty(key, out var v)
                        && !string.IsNullOrWhiteSpace(v.GetString()))
                        return v.GetString()!;
                }
                // ProblemDetails 的校验错误在 errors 字典里
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("errors", out var errors)
                    && errors.ValueKind == JsonValueKind.Object)
                {
                    var msgs = new List<string>();
                    foreach (var p in errors.EnumerateObject())
                    {
                        if (p.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var x in p.Value.EnumerateArray())
                            {
                                var s = x.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) msgs.Add(s!);
                            }
                        }
                        else
                        {
                            var s = p.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) msgs.Add(s!);
                        }
                    }
                    if (msgs.Count > 0) return string.Join("；", msgs);
                }
            }
        }
        catch { }
        return resp.StatusCode.ToString();
    }

    #region 轴承

    /// <summary>
    /// 轴承列表（分页），支持显示已删除项
    /// </summary>
    public async Task<IActionResult> Bearings(string? search, bool includeDeleted = false,
        string? dateFrom = null, string? dateTo = null, string? sourceFilter = null, int page = 1, int pageSize = 20)
    {
        var client = _factory.CreateClient("ApiClient");
        var url = $"{ApiBase()}/api/admin/bearings?page={page}&pageSize={pageSize}&includeDeleted={includeDeleted.ToString().ToLower()}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&keyword={Uri.EscapeDataString(search)}";
        // 改动说明（v1.40.0 下钻筛选）：透传创建时间区间（业务日历 yyyy-MM-dd）与来源给 API
        if (!string.IsNullOrWhiteSpace(dateFrom))
            url += $"&createdFrom={Uri.EscapeDataString(dateFrom)}";
        if (!string.IsNullOrWhiteSpace(dateTo))
            url += $"&createdTo={Uri.EscapeDataString(dateTo)}";
        if (string.Equals(sourceFilter, "internet", StringComparison.OrdinalIgnoreCase))
            url += "&internetSource=true";
        else if (string.Equals(sourceFilter, "noninternet", StringComparison.OrdinalIgnoreCase))
            url += "&internetSource=false";
        var resp = await client.GetAsync(url);
        // 改动说明（v1.49.1 去建档可用性修复）：新建弹窗需要品牌/类型下拉与三项尺寸，
        //   列表页同时预取两份字典数据，避免管理员点"去建档"后遇到必失败的表单
        var brands = await LoadBrandsAsync(client);
        var bearingTypes = await LoadBearingTypesAsync(client);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync();
            _logger.LogWarning("API 请求失败: {Status} {Url} {Body}", resp.StatusCode, url, errBody);
            return View(new BearingListViewModel { Brands = brands, BearingTypes = bearingTypes });
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
            IncludeDeleted = includeDeleted,
            DateFrom = dateFrom,
            DateTo = dateTo,
            SourceFilter = sourceFilter,
            Brands = brands,
            BearingTypes = bearingTypes
        });
    }

    /// <summary>
    /// 拉取品牌字典（新建轴承弹窗下拉），失败只记日志并返回空列表
    /// 改动说明（v1.49.1）：API 建档强制 BrandId，文本品牌名无法满足契约
    /// </summary>
    private async Task<List<BrandItemDto>> LoadBrandsAsync(HttpClient client)
    {
        try
        {
            var resp = await client.GetAsync($"{ApiBase()}/api/admin/brands");
            if (!resp.IsSuccessStatusCode) return [];
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResponse<List<BrandItemDto>>>(json, JsonOpts)?.Data ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "拉取品牌列表失败");
            return [];
        }
    }

    /// <summary>
    /// 拉取轴承类型字典（新建轴承弹窗下拉），失败只记日志并返回空列表
    /// </summary>
    private async Task<List<BearingTypeItemDto>> LoadBearingTypesAsync(HttpClient client)
    {
        try
        {
            var resp = await client.GetAsync($"{ApiBase()}/api/admin/bearing-types");
            if (!resp.IsSuccessStatusCode) return [];
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResponse<List<BearingTypeItemDto>>>(json, JsonOpts)?.Data ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "拉取轴承类型列表失败");
            return [];
        }
    }

    /// <summary>
    /// 创建轴承，POST /api/admin/bearings
    /// 改动说明（v1.49.1 去建档可用性修复）：补齐 API 契约要求的 BrandId/BearingTypeId 与三项尺寸，
    ///   原实现只发文本品牌名与类型名，校验必然失败（新建功能长期不可用）
    /// </summary>
    [HttpPost]
    [PanelPermission("bearing.create")]
    public async Task<IActionResult> CreateBearing(string partNumber, string? oldNumber, string? description,
        Guid brandId, Guid bearingTypeId, string bearingType,
        decimal innerDiameter, decimal outerDiameter, decimal width)
    {
        var client = _factory.CreateClient("ApiClient");
        var body = new
        {
            partNumber,
            oldNumber,
            description,
            bearingType,
            brandId,
            bearingTypeId,
            innerDiameter,
            outerDiameter,
            width
        };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PostAsync($"{ApiBase()}/api/admin/bearings", content);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode
            ? "轴承已创建"
            : $"创建失败: {await ReadErrorAsync(resp)}";
        return RedirectToAction("Bearings");
    }

    /// <summary>
    /// 编辑轴承：先更新文本字段（PUT /api/admin/bearings/{id}），再按需上传/替换/清空主图
    /// （POST /api/admin/bearings/{id}/images，multipart）。主图属平台资产，运营直接维护、不走纠错审核。
    /// </summary>
    [HttpPost]
    [PanelPermission("bearing.edit")]
    public async Task<IActionResult> EditBearing(Guid id, string? partNumber, string? oldNumber, string? description,
        string? brandName, string? bearingType, IFormFile? image3D, IFormFile? image2D,
        bool clearImage3D = false, bool clearImage2D = false)
    {
        var client = _factory.CreateClient("ApiClient");

        // 文本字段先保存：失败时不再提交图片，避免出现"图改了、文本没改"的半状态
        var body = new { partNumber, oldNumber, description, brandName, bearingType };
        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PutAsync($"{ApiBase()}/api/admin/bearings/{id}", content);
        if (!resp.IsSuccessStatusCode)
        {
            TempData["Error"] = $"轴承更新失败: {await ReadErrorAsync(resp)}";
            return RedirectToAction("Bearings");
        }

        var hasImage3D = image3D is { Length: > 0 };
        var hasImage2D = image2D is { Length: > 0 };
        if (hasImage3D || hasImage2D || clearImage3D || clearImage2D)
        {
            // 改动说明（v1.49.2）：图片走 API 的对象存储端点（Admin 不直连业务库、也没有存储实现）
            using var form = new MultipartFormDataContent();
            if (hasImage3D)
            {
                var stream3D = image3D!.OpenReadStream();
                var file3D = new StreamContent(stream3D);
                file3D.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    image3D.ContentType ?? "application/octet-stream");
                form.Add(file3D, "image3D", image3D.FileName);
            }
            if (hasImage2D)
            {
                var stream2D = image2D!.OpenReadStream();
                var file2D = new StreamContent(stream2D);
                file2D.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    image2D.ContentType ?? "application/octet-stream");
                form.Add(file2D, "image2D", image2D.FileName);
            }
            form.Add(new StringContent(clearImage3D ? "true" : "false"), "clearImage3D");
            form.Add(new StringContent(clearImage2D ? "true" : "false"), "clearImage2D");

            var imageResp = await client.PostAsync($"{ApiBase()}/api/admin/bearings/{id}/images", form);
            if (!imageResp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"轴承信息已更新，但图片更新失败: {await ReadErrorAsync(imageResp)}";
                return RedirectToAction("Bearings");
            }
        }

        TempData["Success"] = "轴承已更新";
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

    #region 型号线索

    /// <summary>
    /// 型号线索列表（分页）
    /// 改动说明（v1.49.0 型号线索）：展示发布寻货/商家应答中出现、但平台型号库未收录的型号，
    /// 按出现次数累积；状态为计算态（待建档 / 已忽略 / 已入库），供管理员判断是否建档
    /// 改动说明（v1.49.1）：收紧为 bearing.view（与 API 端点一致，原控制器级 OR 权限会让只有
    ///   merchant.view 的管理员进来看到"暂无线索"，实际是 403 被吞成空列表）
    /// </summary>
    [PanelPermission("bearing.view")]
    public async Task<IActionResult> BearingLeads(string? status = "pending", string? search = null, int page = 1, int pageSize = 20)
    {
        // 改动说明（v1.49.1）：非法 status 回落到 pending——原实现原样透传会让 API 静默返回"全部"，
        //   而视图仍按非法值渲染下拉，出现"显示待建档、数据却是全部"的错位
        var statusValue = string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim().ToLowerInvariant();
        if (statusValue is not ("pending" or "ignored" or "resolved" or "all"))
            statusValue = "pending";
        var apiStatus = statusValue == "all" ? null : statusValue;

        // 改动说明（v1.49.1）：分页夹取 1..100，与 API 侧一致，避免 pageSize=0/超大时 TotalPages 错位
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var client = _factory.CreateClient("ApiClient");
        var url = $"{ApiBase()}/api/admin/bearing-leads?page={page}&pageSize={pageSize}";
        if (apiStatus != null)
            url += $"&status={Uri.EscapeDataString(apiStatus)}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&keyword={Uri.EscapeDataString(search)}";

        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync();
            _logger.LogWarning("型号线索请求失败: {Status} {Url} {Body}", resp.StatusCode, url, errBody);
            // 改动说明（v1.49.1）：失败必须显式提示，不能与"确实没有线索"共用空态
            TempData["Error"] = $"型号线索加载失败：{await ReadErrorAsync(resp)}";
            return View(new BearingLeadListViewModel { Status = statusValue, Search = search, Page = page, PageSize = pageSize });
        }

        var json = await resp.Content.ReadAsStringAsync();
        var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<BearingLeadItemDto>>>(json, JsonOpts);
        return View(new BearingLeadListViewModel
        {
            Items = apiResp?.Data?.Items ?? [],
            TotalCount = apiResp?.Data?.TotalCount ?? 0,
            Page = page,
            PageSize = pageSize,
            Status = statusValue,
            Search = search
        });
    }

    /// <summary>
    /// 忽略型号线索，POST /api/admin/bearing-leads/{id}/ignore
    /// 改动说明（v1.49.0 型号线索）：忽略只影响待建档列表展示，出现次数仍继续累积，可随时恢复
    /// </summary>
    [HttpPost]
    [PanelPermission("bearing.edit")]
    public async Task<IActionResult> IgnoreLead(Guid id, string? status, string? search, int page = 1)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.PostAsync($"{ApiBase()}/api/admin/bearing-leads/{id}/ignore", null);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode
            ? "已忽略该型号线索"
            : $"忽略失败：{await ReadErrorAsync(resp)}";
        return RedirectToAction("BearingLeads", new { status, search, page });
    }

    /// <summary>
    /// 恢复型号线索，POST /api/admin/bearing-leads/{id}/restore
    /// </summary>
    [HttpPost]
    [PanelPermission("bearing.edit")]
    public async Task<IActionResult> RestoreLead(Guid id, string? status, string? search, int page = 1)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.PostAsync($"{ApiBase()}/api/admin/bearing-leads/{id}/restore", null);
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode
            ? "已恢复该型号线索"
            : $"恢复失败：{await ReadErrorAsync(resp)}";
        return RedirectToAction("BearingLeads", new { status, search, page });
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
    public async Task<IActionResult> Merchants(string? search, string? verifiedOnly, bool includeDeleted = false,
        string? dateField = null, string? dateFrom = null, string? dateTo = null, string? sourceFilter = null,
        string? sortBy = null, string? sortOrder = null, int page = 1, int pageSize = 20)
    {
        var client = _factory.CreateClient("ApiClient");
        var url = $"{ApiBase()}/api/admin/merchants?page={page}&pageSize={pageSize}&includeDeleted={includeDeleted.ToString().ToLower()}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&keyword={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(verifiedOnly))
            url += $"&verifiedOnly={verifiedOnly}";
        // 改动说明（v1.40.0 时间/来源筛选）：时间字段映射到注册/入驻区间（业务日历 yyyy-MM-dd），来源映射到 internetSource
        var useOnboarded = string.Equals(dateField, "onboarded", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(dateFrom))
            url += $"&{(useOnboarded ? "onboardedFrom" : "registeredFrom")}={Uri.EscapeDataString(dateFrom)}";
        if (!string.IsNullOrWhiteSpace(dateTo))
            url += $"&{(useOnboarded ? "onboardedTo" : "registeredTo")}={Uri.EscapeDataString(dateTo)}";
        if (string.Equals(sourceFilter, "internet", StringComparison.OrdinalIgnoreCase))
            url += "&internetSource=true";
        else if (string.Equals(sourceFilter, "noninternet", StringComparison.OrdinalIgnoreCase))
            url += "&internetSource=false";
        if (!string.IsNullOrWhiteSpace(sortBy))
            url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
        if (!string.IsNullOrWhiteSpace(sortOrder))
            url += $"&sortOrder={Uri.EscapeDataString(sortOrder)}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
            return View(new MerchantListViewModel());

        var json = await resp.Content.ReadAsStringAsync();
        var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<MerchantItemDto>>>(json, JsonOpts);
        // 加载价格配置供在售商品弹窗消费
        var priceConfig = await _priceConfig.GetAsync();
        ViewBag.PriceConfig = priceConfig;
        return View(new MerchantListViewModel
        {
            Items = apiResp?.Data?.Items ?? [],
            TotalCount = apiResp?.Data?.TotalCount ?? 0,
            Page = page,
            PageSize = pageSize,
            Search = search,
            VerifiedOnly = verifiedOnly,
            IncludeDeleted = includeDeleted,
            DateField = dateField,
            DateFrom = dateFrom,
            DateTo = dateTo,
            SourceFilter = sourceFilter,
            SortBy = sortBy,
            SortOrder = sortOrder
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

    /// <summary>
    /// 解除商户归属（v1.24.0），POST /api/admin/merchants/{id}/detach——
    /// 认领商户清场回公海（可再认领、随互联网自然更新）；自建商户会被 API 拒绝（应走删除）。
    /// 上游 400 文案透传 TempData（如"自助创建或提名新建的商户无公海数据可退回"）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DetachMerchant(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.PostAsync($"{ApiBase()}/api/admin/merchants/{id}/detach", null);
        if (resp.IsSuccessStatusCode)
        {
            TempData["Success"] = "已解除该商户归属并退回公开信息池";
        }
        else
        {
            string detail = string.Empty;
            try
            {
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                detail = doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() ?? "" : body;
            }
            catch { }
            TempData["Error"] = string.IsNullOrEmpty(detail) ? $"解除失败: {resp.StatusCode}" : $"解除失败：{detail}";
        }
        return RedirectToAction("Merchants");
    }

    [HttpGet]
    public async Task<IActionResult> GetMerchantBearings(Guid id, bool onlyOnSale = true, int page = 1, int pageSize = 20, string? sortBy = null, string? sortOrder = null)
    {
        var client = _factory.CreateClient("ApiClient");
        try
        {
            // 价格排序受 Price.NumericForSorting 配置控制
            if (string.Equals(sortBy, "price", StringComparison.OrdinalIgnoreCase))
            {
                var priceCfg = await _priceConfig.GetAsync();
                if (!priceCfg.NumericForSorting)
                {
                    return Json(new { success = false, message = "价格排序功能已关闭" });
                }
            }

            var url = $"{ApiBase()}/api/merchants/{id}/bearings?page={page}&pageSize={pageSize}";
            if (onlyOnSale) url += "&onlyOnSale=true";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={sortBy}";
            if (!string.IsNullOrEmpty(sortOrder)) url += $"&sortOrder={sortOrder}";

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

}
