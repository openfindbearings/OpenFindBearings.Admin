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

        // Parse items（兼容各项目 DTO 字段命名差异：Sync 用 operatorId/operatorName/resourceType/resourceId/ipAddress/createdAt，Identity 用 userId/username，API 用 entityType/entityId/entityName/operatedAt）
        if (data.TryGetProperty("items", out var itemsEl))
        {
            foreach (var item in itemsEl.EnumerateArray())
            {
                var details = GetString(item, "details", "remarks");
                ParseDetails(details, out var detailMethod, out var detailPath, out var statusCode, out var durationMs);

                items.Add(new AuditLogItemDto(
                    Id: GetGuid(item, "id") ?? Guid.Empty,
                    UserId: GetGuid(item, "operatorId", "userId"),
                    Username: GetString(item, "operatorName", "username"),
                    Action: GetString(item, "action") ?? "",
                    ResourceType: GetString(item, "resourceType", "entityType"),
                    ResourceId: GetResourceId(item),
                    Details: details,
                    Status: GetString(item, "status"),
                    FailureReason: GetString(item, "failureReason"),
                    ClientId: GetString(item, "clientId"),
                    IpAddress: GetString(item, "ipAddress"),
                    UserAgent: GetString(item, "userAgent"),
                    CreatedAt: GetDateTime(item, "createdAt", "operatedAt"),
                    HttpMethod: GetString(item, "requestMethod") ?? detailMethod,
                    RequestPath: GetString(item, "requestPath") ?? detailPath,
                    StatusCode: statusCode,
                    DurationMs: durationMs
                ));
            }
        }

        if (data.TryGetProperty("totalCount", out var tc))
            totalCount = tc.GetInt32();

        return (items, totalCount);
    }

    private static string? GetString(System.Text.Json.JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var value) &&
                value.ValueKind == System.Text.Json.JsonValueKind.String)
                return value.GetString();
        }
        return null;
    }

    private static Guid? GetGuid(System.Text.Json.JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var value) &&
                value.ValueKind == System.Text.Json.JsonValueKind.String &&
                value.TryGetGuid(out var guid))
                return guid;
        }
        return null;
    }

    private static DateTimeOffset GetDateTime(System.Text.Json.JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var value) &&
                value.TryGetDateTimeOffset(out var dto))
                return dto;
        }
        return DateTimeOffset.MinValue;
    }

    private static string? GetResourceId(System.Text.Json.JsonElement item)
    {
        if (item.TryGetProperty("resourceId", out var rid) &&
            rid.ValueKind == System.Text.Json.JsonValueKind.String)
            return rid.GetString();

        if (item.TryGetProperty("entityName", out var en) &&
            en.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var name = en.GetString();
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        if (item.TryGetProperty("entityId", out var eid) &&
            eid.ValueKind == System.Text.Json.JsonValueKind.String)
            return eid.GetString();

        return null;
    }

    private static void ParseDetails(string? details, out string? method, out string? path, out int? statusCode, out long? durationMs)
    {
        method = null;
        path = null;
        statusCode = null;
        durationMs = null;

        if (string.IsNullOrEmpty(details))
            return;

        var m = System.Text.RegularExpressions.Regex.Match(details, @"^(\w+)\s+(.+?)\s*->\s*(\d+)");
        if (m.Success)
        {
            method = m.Groups[1].Value;
            path = m.Groups[2].Value.Trim();
            if (int.TryParse(m.Groups[3].Value, out var sc))
                statusCode = sc;
        }

        var d = System.Text.RegularExpressions.Regex.Match(details, @"\((\d+)ms\)");
        if (d.Success && long.TryParse(d.Groups[1].Value, out var dm))
            durationMs = dm;
    }
}
