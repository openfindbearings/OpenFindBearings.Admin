using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 商城目录管理控制器（v2.3.0 商城虚拟权益）：代理 API /api/admin/mall/items。
/// 列表 system.view 可见；编辑/上下架 system.manage（复用既有键，免新增权限迁移）。
/// 价格与闪购窗口实时读不缓存——改完即生效，无需发版
/// </summary>
[Authorize]
[PanelPermission("system.view")]
public class MallController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<MallController> _logger;

    public MallController(IHttpClientFactory factory, IConfiguration config, ILogger<MallController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    /// <summary>商城目录列表页（含停用项与已售数量）</summary>
    public async Task<IActionResult> Index()
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.GetAsync($"{apiBase}/api/admin/mall/items");
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var d))
                {
                    var items = d.EnumerateArray().Select(x => new MallItemRowVm
                    {
                        Id = x.GetProperty("id").GetGuid(),
                        Key = x.GetProperty("key").GetString() ?? "",
                        Name = x.GetProperty("name").GetString() ?? "",
                        Description = x.GetProperty("description").GetString() ?? "",
                        Icon = x.GetProperty("icon").GetString() ?? "",
                        Category = x.GetProperty("category").GetInt32(),
                        PointPrice = x.GetProperty("pointPrice").GetInt32(),
                        FlashPrice = NumOrNull(x, "flashPrice"),
                        FlashStart = DateOrNull(x, "flashStart"),
                        FlashEnd = DateOrNull(x, "flashEnd"),
                        DurationHours = NumOrNull(x, "durationHours"),
                        Stock = x.GetProperty("stock").GetInt32(),
                        SoldCount = x.GetProperty("soldCount").GetInt32(),
                        Enabled = x.GetProperty("enabled").GetBoolean(),
                        SortOrder = x.GetProperty("sortOrder").GetInt32()
                    }).ToList();
                    return View(items);
                }
            }
            TempData["Error"] = $"商城目录加载失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "商城目录拉取异常");
            TempData["Error"] = $"商城目录拉取失败: {ex.Message}";
        }
        return View(new List<MallItemRowVm>());
    }

    /// <summary>编辑商品（价格/闪购窗口/库存/时长/上下架/文案/排序）</summary>
    [HttpPost]
    [PanelPermission("system.manage")]
    public async Task<IActionResult> Update(Guid id, string name, string description, string icon,
        int pointPrice, int? flashPrice, string? flashStartUtc, string? flashEndUtc,
        int? durationHours, int stock, int sortOrder, bool enabled)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var payload = new
            {
                name,
                description,
                icon,
                pointPrice,
                flashPrice,
                // 改动说明（时区规则）：服务端不做时区转换——闪购窗口由前端 JS 把本地输入转成
                //   UTC ISO 串（隐藏域 flashStartUtc/flashEndUtc）后提交，这里只按 UTC 解析透传。
                //   未设闪购价则窗口一并清空，避免残留窗口影响后续改价
                flashStart = flashPrice.HasValue ? ParseUtc(flashStartUtc) : (DateTime?)null,
                flashEnd = flashPrice.HasValue ? ParseUtc(flashEndUtc) : (DateTime?)null,
                durationHours,
                stock,
                enabled,
                sortOrder
            };
            var resp = await client.PutAsJsonAsync($"{apiBase}/api/admin/mall/items/{id}", payload);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] =
                resp.IsSuccessStatusCode ? "商品已保存" : $"保存失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"保存失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>读取可空整数（缺省/null 安全）</summary>
    private static int? NumOrNull(JsonElement x, string prop) =>
        x.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : (int?)null;

    /// <summary>读取可空时间（保持 UTC 原值，展示层转换交给浏览器 JS，服务端不转时区）</summary>
    private static DateTime? DateOrNull(JsonElement x, string prop) =>
        x.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String &&
        DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : (DateTime?)null;

    /// <summary>解析前端提交的 UTC ISO 串（空/非法返回 null）</summary>
    private static DateTime? ParseUtc(string? s) =>
        !string.IsNullOrWhiteSpace(s) &&
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : (DateTime?)null;
}

/// <summary>商城商品行视图模型</summary>
public class MallItemRowVm
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public int Category { get; set; }
    public int PointPrice { get; set; }
    public int? FlashPrice { get; set; }
    public DateTime? FlashStart { get; set; }
    public DateTime? FlashEnd { get; set; }
    public int? DurationHours { get; set; }
    public int Stock { get; set; }
    public int SoldCount { get; set; }
    public bool Enabled { get; set; }
    public int SortOrder { get; set; }
}
