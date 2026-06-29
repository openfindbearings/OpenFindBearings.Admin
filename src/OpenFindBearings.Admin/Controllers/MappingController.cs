using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

[Authorize]
public class MappingController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<MappingController> _logger;

    public MappingController(IHttpClientFactory factory, IConfiguration config, ILogger<MappingController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    private string SyncBase => _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";

    public async Task<IActionResult> Index(string type = "brand", int page = 1)
    {
        ViewBag.MappingType = type;
        ViewBag.Page = page;
        var client = _factory.CreateClient("SyncClient");
        var url = type == "brand"
            ? $"{SyncBase}/api/config/brands?page={page}&pageSize=20"
            : $"{SyncBase}/api/config/types?page={page}&pageSize=20";

        try
        {
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("映射接口返回 {StatusCode}", resp.StatusCode);
                ViewBag.Error = "Sync 服务可能离线";
                return View();
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
            {
                ViewBag.Error = "获取映射数据失败";
                return View();
            }

            var items = new List<object>();
            var totalCount = 0;
            var pageIndex = 1;
            var totalPages = 1;

            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("items", out var itemsEl))
                {
                    foreach (var item in itemsEl.EnumerateArray())
                        items.Add(item);
                }
                if (data.TryGetProperty("totalCount", out var tc))
                    totalCount = tc.GetInt32();
                if (data.TryGetProperty("pageIndex", out var pi))
                    pageIndex = pi.GetInt32();
                if (data.TryGetProperty("totalPages", out var tp))
                    totalPages = tp.GetInt32();
            }

            ViewBag.Items = items;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageIndex = pageIndex;
            ViewBag.PageSize = 20;
            ViewBag.TotalPages = totalPages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取映射列表异常");
            ViewBag.Error = "连接 Sync 服务异常";
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(string type, string standardCode, string standardName, string alias, int confidence = 100)
    {
        var client = _factory.CreateClient("SyncClient");
        var body = new { standardCode, standardName, alias, confidence };
        var url = type == "brand"
            ? $"{SyncBase}/api/config/brands"
            : $"{SyncBase}/api/config/types";

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(url, content);
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
                return Json(new { success = true, message = "创建成功" });

            _logger.LogWarning("创建映射失败: {StatusCode} {Response}", resp.StatusCode, json);
            return Json(new { success = false, message = "创建失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建映射异常");
            return Json(new { success = false, message = "服务异常" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string type, Guid id, string standardCode, string standardName, string alias, int confidence, bool isActive)
    {
        var client = _factory.CreateClient("SyncClient");
        var body = new { standardCode, standardName, alias, confidence, isActive };
        var url = type == "brand"
            ? $"{SyncBase}/api/config/brands/{id}"
            : $"{SyncBase}/api/config/types/{id}";

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var resp = await client.PutAsync(url, content);
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
                return Json(new { success = true, message = "更新成功" });

            _logger.LogWarning("更新映射失败: {StatusCode} {Response}", resp.StatusCode, json);
            return Json(new { success = false, message = "更新失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新映射异常");
            return Json(new { success = false, message = "服务异常" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string type, Guid id)
    {
        var client = _factory.CreateClient("SyncClient");
        var url = type == "brand"
            ? $"{SyncBase}/api/config/brands/{id}"
            : $"{SyncBase}/api/config/types/{id}";

        try
        {
            var resp = await client.DeleteAsync(url);
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
                return Json(new { success = true, message = "删除成功" });

            _logger.LogWarning("删除映射失败: {StatusCode} {Response}", resp.StatusCode, json);
            return Json(new { success = false, message = "删除失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除映射异常");
            return Json(new { success = false, message = "服务异常" });
        }
    }
}
