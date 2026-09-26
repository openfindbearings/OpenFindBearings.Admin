using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.ViewModels;
using System.Text.Json;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 角色管理控制器（v1.25.0 改代理 API RBAC）。
/// 改动说明：原实现读写 Admin 本地 db_admin 的 AdminRolePermissions 僵尸表（改了零效果）；
/// 现全部代理 API /api/admin/roles*（业务权限 API 一家管原则），视图契约保持不变。
/// </summary>
[Authorize]
[PanelPermission("role.manage")]
public class RoleController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<RoleController> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// 构造：注入 HTTP 工厂与配置
    /// </summary>
    public RoleController(IHttpClientFactory factory, IConfiguration config, ILogger<RoleController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    private string ApiBase() => _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";

    private HttpClient Api() => _factory.CreateClient("ApiClient");

    /// <summary>
    /// 解包 API 响应 data 节点（ApiResponse 包装：success/message/data）
    /// </summary>
    private static JsonElement? DataOf(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("data", out var d) ? d.Clone() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 角色列表（代理 API 分页角色，取全量一页）
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var resp = await Api().GetAsync($"{ApiBase()}/api/admin/roles?page=1&pageSize=100");
        var roles = new List<RoleViewModel>();
        if (resp.IsSuccessStatusCode)
        {
            var data = DataOf(await resp.Content.ReadAsStringAsync());
            if (data?.TryGetProperty("items", out var items) == true)
            {
                roles = items.EnumerateArray().Select(x => new RoleViewModel
                {
                    RoleName = x.GetProperty("name").GetString() ?? "",
                    // 改动说明（v1.29.0）：中文显示名透传（Name 为英文机器标识）
                    DisplayName = x.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String ? dn.GetString() : null,
                    PermissionCount = x.TryGetProperty("permissions", out var p) && p.ValueKind == JsonValueKind.Array ? p.GetArrayLength() : 0,
                }).ToList();
            }
        }
        else
        {
            TempData["Error"] = $"角色列表加载失败: {resp.StatusCode}";
        }

        ViewBag.Items = roles;
        return View();
    }

    /// <summary>
    /// 角色权限详情（权限点目录只读清单 + 该角色已授权勾选）
    /// </summary>
    public async Task<IActionResult> Permissions(string roleName)
    {
        if (string.IsNullOrEmpty(roleName))
            return RedirectToAction("Index");

        ViewBag.RoleName = roleName;

        // 角色名 → ID（API 权限分配以 ID 为键）
        var roleId = await ResolveRoleIdAsync(roleName);
        var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roleId.HasValue)
        {
            var resp = await Api().GetAsync($"{ApiBase()}/api/admin/roles/{roleId}/permissions");
            if (resp.IsSuccessStatusCode)
            {
                var data = DataOf(await resp.Content.ReadAsStringAsync());
                if (data?.ValueKind == JsonValueKind.Array)
                    granted = data.Value.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        // 权限点目录（只读清单来源=API 权限表，界面不再允许新增权限点）
        var catalog = new List<PermissionItemViewModel>();
        var catResp = await Api().GetAsync($"{ApiBase()}/api/admin/permissions?page=1&pageSize=200");
        if (catResp.IsSuccessStatusCode)
        {
            var data = DataOf(await catResp.Content.ReadAsStringAsync());
            if (data?.TryGetProperty("items", out var items) == true)
            {
                catalog = items.EnumerateArray().Select(x => new PermissionItemViewModel
                {
                    Key = x.GetProperty("name").GetString() ?? "",
                    Granted = granted.Contains(x.GetProperty("name").GetString() ?? "")
                }).ToList();
            }
        }

        return View(catalog);
    }

    /// <summary>
    /// 创建角色（代理 API；权限后续在详情页勾选）
    /// 改动说明（v1.29.0）：加 displayName——英文标识 Name 为鉴权键（API 正则校验），
    /// 中文人读名走 DisplayName；前端预检英文标识给友好提示，免撞 API 400
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(string roleName, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            TempData["Error"] = "角色标识不能为空";
            return RedirectToAction("Index");
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(roleName.Trim(), "^[A-Za-z][A-Za-z0-9_]*$"))
        {
            TempData["Error"] = "角色标识仅允许英文字母开头（字母/数字/下划线），中文名称请填\"显示名称\"";
            return RedirectToAction("Index");
        }

        var resp = await Api().PostAsJsonAsync($"{ApiBase()}/api/admin/roles", new { name = roleName.Trim(), description = (string?)null, displayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim() });
        if (resp.IsSuccessStatusCode)
            TempData["Success"] = $"角色 '{roleName}' 创建成功";
        else
            TempData["Error"] = await ErrorTextAsync(resp, "创建失败");

        return RedirectToAction("Permissions", new { roleName = roleName.Trim() });
    }

    /// <summary>
    /// 删除角色（代理 API；系统角色由 API 守卫拒绝并透传原因）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(string roleName)
    {
        if (string.IsNullOrEmpty(roleName))
            return RedirectToAction("Index");

        var roleId = await ResolveRoleIdAsync(roleName);
        if (!roleId.HasValue)
        {
            TempData["Error"] = $"角色 '{roleName}' 不存在";
            return RedirectToAction("Index");
        }

        var resp = await Api().DeleteAsync($"{ApiBase()}/api/admin/roles/{roleId}");
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] =
            resp.IsSuccessStatusCode ? $"角色 '{roleName}' 已删除" : await ErrorTextAsync(resp, "删除失败");
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 保存角色权限（整表覆盖语义：勾选清单整体提交，API 侧替换式分配）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SavePermissions(string roleName, List<string> grantedPermissions)
    {
        if (string.IsNullOrEmpty(roleName))
            return RedirectToAction("Index");

        var roleId = await ResolveRoleIdAsync(roleName);
        if (!roleId.HasValue)
        {
            TempData["Error"] = $"角色 '{roleName}' 不存在";
            return RedirectToAction("Index");
        }

        var resp = await Api().PostAsJsonAsync($"{ApiBase()}/api/admin/roles/{roleId}/permissions",
            new { permissionNames = grantedPermissions ?? new List<string>() });
        TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] =
            resp.IsSuccessStatusCode ? "权限已保存" : await ErrorTextAsync(resp, "保存失败");
        return RedirectToAction("Permissions", new { roleName });
    }

    /// <summary>
    /// 角色名解析为 API 角色 ID（/roles/all 全量清单匹配）
    /// </summary>
    private async Task<Guid?> ResolveRoleIdAsync(string roleName)
    {
        var resp = await Api().GetAsync($"{ApiBase()}/api/admin/roles/all");
        if (!resp.IsSuccessStatusCode) return null;
        var data = DataOf(await resp.Content.ReadAsStringAsync());
        if (data?.ValueKind != JsonValueKind.Array) return null;
        foreach (var x in data.Value.EnumerateArray())
        {
            if (string.Equals(x.GetProperty("name").GetString(), roleName, StringComparison.OrdinalIgnoreCase))
                return x.GetProperty("id").GetGuid();
        }

        return null;
    }

    /// <summary>
    /// 提取 API 错误文案（Problem/detail 或 message 字段），失败回退状态码
    /// </summary>
    private async Task<string> ErrorTextAsync(HttpResponseMessage resp, string fallback)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m) && !string.IsNullOrEmpty(m.GetString()))
                return $"{fallback}：{m.GetString()}";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "解析 API 错误响应失败");
        }

        return $"{fallback}: {resp.StatusCode}";
    }
}
