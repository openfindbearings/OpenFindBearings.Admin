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

    /// <summary>商城管理页（v2.4.0 三区：平台权益目录 / 商家挂礼待审 / 托管中礼品单）</summary>
    public async Task<IActionResult> Index()
    {
        var vm = new MallIndexVm();
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
                    vm.Items = d.EnumerateArray().Select(x => new MallItemRowVm
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
                        SortOrder = x.GetProperty("sortOrder").GetInt32(),
                        // 改动说明（v2.10.0 寻货置顶）：置顶对象类型透传（列表列+编辑下拉数据源）
                        TargetKind = x.TryGetProperty("targetKind", out var tk) ? tk.GetInt32() : 1
                    }).ToList();
                }
            }
            else TempData["Error"] = $"商城目录加载失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "商城目录拉取异常");
            TempData["Error"] = $"商城目录拉取失败: {ex.Message}";
        }

        // v2.4.0：待审挂礼队列（merchant.verify）+ 托管中礼品单（merchant.manage）
        vm.Gifts = await GetArrayAsync(apiBase, client, "/api/admin/mall/gifts/pending", x => new GiftRowVm
        {
            Id = x.GetProperty("id").GetGuid(),
            Name = x.GetProperty("name").GetString() ?? "",
            Description = x.GetProperty("description").GetString() ?? "",
            ImageKey = x.GetProperty("imageKey").GetString() ?? "",
            Stock = x.GetProperty("stock").GetInt32(),
            OwnerMerchantName = x.GetProperty("ownerMerchantName").GetString() ?? "",
            CreatedAt = ParseUtc(x.GetProperty("createdAt").GetString()) ?? DateTime.MinValue
        });
        vm.Orders = await GetArrayAsync(apiBase, client, "/api/admin/mall/orders/escrow", x => new EscrowOrderRowVm
        {
            Id = x.GetProperty("id").GetGuid(),
            ItemName = x.GetProperty("itemName").GetString() ?? "",
            PointsSpent = x.GetProperty("pointsSpent").GetInt32(),
            ShipStatus = x.GetProperty("shipStatus").GetInt32(),
            ReceiverName = x.GetProperty("receiverName").GetString() ?? "",
            ReceiverPhone = x.GetProperty("receiverPhone").GetString() ?? "",
            CreatedAt = ParseUtc(x.GetProperty("createdAt").GetString()) ?? DateTime.MinValue
        });
        return View(vm);
    }

    /// <summary>拉取并映射 API 数组载荷（失败回空表，不阻断整页——各区独立降级）</summary>
    private static async Task<List<T>> GetArrayAsync<T>(string apiBase, HttpClient client, string path, Func<JsonElement, T> map)
    {
        var list = new List<T>();
        try
        {
            var resp = await client.GetAsync(apiBase + path);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var d))
                    list.AddRange(d.EnumerateArray().Select(map));
            }
        }
        catch
        {
            // 降级空表：礼品审核/订单区无数据时给空态提示即可
        }
        return list;
    }

    /// <summary>挂礼审核通过并定档（平台统一定价）</summary>
    [HttpPost]
    [PanelPermission("merchant.verify")]
    public async Task<IActionResult> ApproveGift(Guid id, int pointPrice)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsJsonAsync($"{apiBase}/api/admin/mall/gifts/{id}/approve", new { pointPrice });
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode
                ? "已通过并定档"
                : $"审核失败: {(int)resp.StatusCode}（价格超封顶或状态不符）";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"审核失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>挂礼审核驳回（原因商户可见）</summary>
    [HttpPost]
    [PanelPermission("merchant.verify")]
    public async Task<IActionResult> RejectGift(Guid id, string? reason)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsJsonAsync($"{apiBase}/api/admin/mall/gifts/{id}/reject", new { reason });
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode
                ? "已驳回" : $"驳回失败: {(int)resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"驳回失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>托管礼品单争议退款（原路退分给买家）</summary>
    [HttpPost]
    [PanelPermission("merchant.manage")]
    public async Task<IActionResult> RefundOrder(Guid id, string? reason)
    {
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsJsonAsync($"{apiBase}/api/admin/mall/orders/{id}/refund", new { reason });
            // API 用 Problem/BadRequest 承载业务拒绝原因，透传给管理员
            if (resp.IsSuccessStatusCode)
            {
                TempData["Success"] = "已退款";
            }
            else
            {
                var body = await resp.Content.ReadAsStringAsync();
                var msg = body.Contains("\"detail\"")
                    ? System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString()
                    : $"退款失败: {(int)resp.StatusCode}";
                TempData["Error"] = msg;
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"退款失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>编辑商品（价格/闪购窗口/库存/时长/上下架/文案/排序）</summary>
    [HttpPost]
    [PanelPermission("system.manage")]
    public async Task<IActionResult> Update(Guid id, string name, string description, string icon,
        int pointPrice, int? flashPrice, string? flashStartUtc, string? flashEndUtc,
        int? durationHours, int stock, int sortOrder, bool enabled,
        // 改动说明（v2.10.0 寻货置顶）：置顶对象类型透传 API（1=商品/2=需求）
        int targetKind = 1)
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
                sortOrder,
                targetKind
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

/// <summary>商城管理页视图模型（三区数据）</summary>
public class MallIndexVm
{
    /// <summary>平台权益目录行</summary>
    public List<MallItemRowVm> Items { get; set; } = new();

    /// <summary>待审商家挂礼</summary>
    public List<GiftRowVm> Gifts { get; set; } = new();

    /// <summary>托管中礼品单（争议退款队列）</summary>
    public List<EscrowOrderRowVm> Orders { get; set; } = new();
}

/// <summary>待审挂礼行</summary>
public class GiftRowVm
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImageKey { get; set; } = "";
    public int Stock { get; set; }
    public string OwnerMerchantName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>托管中礼品单行</summary>
public class EscrowOrderRowVm
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = "";
    public int PointsSpent { get; set; }
    public int ShipStatus { get; set; }
    public string ReceiverName { get; set; } = "";
    public string ReceiverPhone { get; set; } = "";
    public DateTime CreatedAt { get; set; }
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
    // 改动说明（v2.10.0 寻货置顶）：置顶对象类型（1=商品/2=需求，非置顶卡恒 1）
    public int TargetKind { get; set; } = 1;
}
