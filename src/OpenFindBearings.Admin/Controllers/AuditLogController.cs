using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

[Authorize]
public class AuditLogController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<AuditLogController> _logger;

    public AuditLogController(IHttpClientFactory factory, IConfiguration config, ILogger<AuditLogController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string source = "identity", int page = 1, int pageSize = 30)
    {
        ViewBag.Source = source;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Items = new List<AuditLogItemDto>();

        try
        {
            var (items, totalCount) = source switch
            {
                "identity" => await LoadIdentityLogsAsync(page, pageSize),
                "api" => await LoadApiLogsAsync(page, pageSize),
                "sync" => await LoadSyncLogsAsync(page, pageSize),
                _ => ([], 0)
            };

            ViewBag.Items = items;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取审计日志失败: {Source}", source);
            ViewBag.Error = "服务可能离线";
        }

        return View();
    }

    private async Task<(List<AuditLogItemDto>, int)> LoadIdentityLogsAsync(int page, int pageSize)
    {
        var baseUrl = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        var resp = await client.GetAsync($"{baseUrl}/api/auditlog?page={page}&pageSize={pageSize}");

        if (!resp.IsSuccessStatusCode) return ([], 0);

        var json = await resp.Content.ReadAsStringAsync();
        return ParsePagedResponse(json);
    }

    private async Task<(List<AuditLogItemDto>, int)> LoadApiLogsAsync(int page, int pageSize)
    {
        var baseUrl = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var client = _factory.CreateClient("ApiClient");
        var resp = await client.GetAsync($"{baseUrl}/api/admin/audit-logs?page={page}&pageSize={pageSize}");

        if (!resp.IsSuccessStatusCode) return ([], 0);

        var json = await resp.Content.ReadAsStringAsync();
        return ParsePagedResponse(json);
    }

    private async Task<(List<AuditLogItemDto>, int)> LoadSyncLogsAsync(int page, int pageSize)
    {
        var baseUrl = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
        var client = _factory.CreateClient("SyncClient");
        var resp = await client.GetAsync($"{baseUrl}/api/audit-log?page={page}&pageSize={pageSize}");

        if (!resp.IsSuccessStatusCode) return ([], 0);

        var json = await resp.Content.ReadAsStringAsync();
        return ParsePagedResponse(json);
    }

    private (List<AuditLogItemDto>, int) ParsePagedResponse(string json)
    {
        var items = new List<AuditLogItemDto>();
        var totalCount = 0;

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
            return (items, totalCount);

        if (!root.TryGetProperty("data", out var data))
            return (items, totalCount);

        // Parse items
        if (data.TryGetProperty("items", out var itemsEl))
        {
            foreach (var item in itemsEl.EnumerateArray())
            {
                    items.Add(new AuditLogItemDto(
                        Id: item.TryGetProperty("id", out var id) && id.TryGetGuid(out var guid) ? guid : Guid.Empty,
                        UserId: item.TryGetProperty("operatorId", out var uid) && uid.TryGetGuid(out var uGuid) ? uGuid : null,
                        Username: item.TryGetProperty("operatorName", out var un) ? un.GetString() ?? "" : null,
                        Action: item.TryGetProperty("action", out var action) ? action.GetString() ?? "" : "",
                        ResourceType: item.TryGetProperty("resourceType", out var rt) ? rt.GetString() ?? "" : null,
                        ResourceId: item.TryGetProperty("resourceId", out var rid) ? rid.GetString() ?? "" : null,
                        Details: null,
                        Status: null,
                        FailureReason: null,
                        ClientId: null,
                        IpAddress: item.TryGetProperty("ipAddress", out var ip) ? ip.GetString() ?? "" : null,
                        UserAgent: null,
                        CreatedAt: item.TryGetProperty("createdAt", out var ca) && ca.TryGetDateTimeOffset(out var dto) ? dto : DateTimeOffset.MinValue
                    ));
            }
        }

        if (data.TryGetProperty("totalCount", out var tc))
            totalCount = tc.GetInt32();

        return (items, totalCount);
    }
}
