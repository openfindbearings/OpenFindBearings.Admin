using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

[Authorize]
public class MerchantVerifyController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<MerchantVerifyController> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public MerchantVerifyController(IHttpClientFactory factory, IConfiguration config, ILogger<MerchantVerifyController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    private string ApiBase() => _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";

    public async Task<IActionResult> Index(string? status = "pending", string search = "", int page = 1, int pageSize = 20)
    {
        var client = _factory.CreateClient("ApiClient");
        var url = $"{ApiBase()}/api/admin/merchants?page={page}&pageSize={pageSize}&includeDeleted=false&excludeCrawler=true";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&keyword={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusMap = new Dictionary<string, int>
            {
                ["pending"] = 2,
                ["active"] = 0,
                ["suspended"] = 1
            };
            if (statusMap.TryGetValue(status, out var sv))
                url += $"&status={sv}";
        }

        try
        {
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("入驻申请审批 API 请求失败: {Status}", resp.StatusCode);
                ViewBag.Error = "无法获取商家列表";
                return View();
            }

            var json = await resp.Content.ReadAsStringAsync();
            var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<MerchantItemDto>>>(json, JsonOpts);
            ViewBag.Items = apiResp?.Data?.Items ?? [];
            ViewBag.TotalCount = apiResp?.Data?.TotalCount ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取入驻申请审批列表异常");
            ViewBag.Error = "连接 API 服务异常";
        }

        ViewBag.Status = status;
        ViewBag.Search = search;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Approve(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{ApiBase()}/api/admin/merchants/{id}/approve", null);
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("入驻审核通过: {Id}", id);
                return Json(new { success = true, message = "已审核通过，商户已生效" });
            }

            // 改动说明：并发审批冲突透传——API 对"申请已被处理"返回 409 ProblemDetails，
            //   Admin 前端据此提示"该申请已被处理"并刷新列表，而非笼统"操作失败"
            if ((int)resp.StatusCode == 409)
                return Json(new { success = false, conflict = true, message = ExtractProblemDetail(json) ?? "该申请已被其他管理员处理，请刷新" });

            _logger.LogWarning("入驻审核通过失败: {Id}, {StatusCode}, {Response}", id, resp.StatusCode, json);
            return Json(new { success = false, message = ExtractProblemDetail(json) ?? "操作失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "入驻审核通过异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Verify(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.PostAsync($"{ApiBase()}/api/admin/merchants/{id}/verify", null);
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("商家认证成功: {Id}", id);
                return Json(new { success = true, message = "已认证" });
            }

            _logger.LogWarning("商家认证失败: {Id}, {StatusCode}, {Response}", id, resp.StatusCode, json);
            return Json(new { success = false, message = "操作失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "商家认证异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Reject(Guid id, string reason = "")
    {
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var body = new { reason };
            var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{ApiBase()}/api/admin/merchants/{id}/reject", content);
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("商家认证已拒绝: {Id}, Reason={Reason}", id, reason);
                return Json(new { success = true, message = "已拒绝" });
            }

            // 改动说明：与 Approve 对称，409 冲突透传给前端提示并刷新
            if ((int)resp.StatusCode == 409)
                return Json(new { success = false, conflict = true, message = ExtractProblemDetail(json) ?? "该申请已被其他管理员处理，请刷新" });

            _logger.LogWarning("商家拒绝失败: {Id}, {StatusCode}, {Response}", id, resp.StatusCode, json);
            return Json(new { success = false, message = ExtractProblemDetail(json) ?? "操作失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "商家拒绝异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }

    /// <summary>
    /// 商户详情代理：透传 API GET /api/admin/merchants/{id}（含入驻渠道/提交时间/拒绝原因/信用代码等审批抽屉字段）
    /// 改动说明：主流审批后台"先看全资料再决策"，抽屉数据源
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Detail(Guid id)
    {
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var resp = await client.GetAsync($"{ApiBase()}/api/admin/merchants/{id}");
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
                return Content(json, "application/json");

            _logger.LogWarning("获取商户详情失败: {Id}, {StatusCode}", id, resp.StatusCode);
            return Json(new { success = false, message = "获取详情失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取商户详情异常: {Id}", id);
            return Json(new { success = false, message = "服务异常" });
        }
    }

    /// <summary>
    /// 从 API 的 ProblemDetails JSON 中提取 detail 文案（解析失败返回 null 走兜底文案）
    /// </summary>
    private static string? ExtractProblemDetail(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
        }
        catch { return null; }
    }

    [HttpGet]
    public async Task<IActionResult> GetMerchantBearings(Guid id, string? dataSource = null, bool onlyOnSale = false, int pageSize = 9999)
    {
        var client = _factory.CreateClient("ApiClient");
        try
        {
            var url = $"{ApiBase()}/api/merchants/{id}/bearings?pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(dataSource))
                url += $"&dataSource={Uri.EscapeDataString(dataSource)}";
            if (onlyOnSale)
                url += "&onlyOnSale=true";

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
}
