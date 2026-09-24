using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 用户管理控制器（调用 Identity API）
/// </summary>
[Authorize]
[PanelPermission("user.manage")]
public class UsersController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public UsersController(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    /// <summary>
    /// 用户列表
    /// </summary>
    public async Task<IActionResult> Index(string search = "", int page = 1, int pageSize = 20, bool includeDeleted = false)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        var status = includeDeleted ? "" : "enabled";
        var url = $"{identityBase}/api/account/admin/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(status))
            url += $"&status={status}";
        if (includeDeleted)
            url += "&includeDeleted=true";

        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                // 改动说明：原实现用默认 JsonSerializerOptions，Default 配置为大小写敏感。
                //           Identity 实际序列化为 camelCase（与 API 约定一致），字段名全对得上。
                //           显式声明 PropertyNameCaseInsensitive 仍保留作为防御，避免后续字段调整时
                //           静默反序列化为默认值
                var result = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<PagedData<UserItemDto>>>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                ViewBag.Items = result?.Data?.Items ?? [];
                ViewBag.TotalCount = result?.Data?.TotalCount ?? 0;
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
            }
            else
            {
                // 改动说明：失败分支显式写入"空结果"占位，避免视图读 null 抛异常；
                //   并通过 TempData 给出可观测的诊断信息，区分"API 不可达"与"无数据"
                ViewBag.Items = new List<UserItemDto>();
                ViewBag.TotalCount = 0;
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                TempData["Error"] = $"加载用户列表失败: HTTP {(int)resp.StatusCode} {resp.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            ViewBag.Items = new List<UserItemDto>();
            ViewBag.TotalCount = 0;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            TempData["Error"] = $"加载失败: {ex.Message}";
        }

        ViewBag.Search = search;
        ViewBag.IncludeDeleted = includeDeleted;
        return View();
    }

    /// <summary>
    /// 创建用户
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(string userName, string password, string? email, string? name, string? phoneNumber)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var payload = new
            {
                userName,
                password,
                email,
                name,
                phoneNumber
            };
            var resp = await client.PostAsJsonAsync($"{identityBase}/api/account/admin/users", payload);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "用户创建成功" : $"创建失败: {await resp.Content.ReadAsStringAsync()}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"创建失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 启用/禁用用户
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var resp = await client.PatchAsync($"{identityBase}/api/account/admin/users/{id}/status", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "状态已切换" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 解锁用户
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Unlock(string id)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var resp = await client.PostAsync($"{identityBase}/api/account/admin/users/{id}/unlock", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "已解锁" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var payload = new { newPassword };
            var resp = await client.PostAsJsonAsync($"{identityBase}/api/account/admin/users/{id}/reset-password", payload);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "密码已重置" : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 恢复已删除用户
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Restore(string id)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var resp = await client.PostAsync($"{identityBase}/api/account/admin/users/{id}/restore", null);
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "用户已恢复" : $"恢复失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"恢复失败: {ex.Message}";
        }
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 彻底删除用户（物理删除，不可恢复）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HardDelete(string id)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var resp = await client.DeleteAsync($"{identityBase}/api/account/admin/users/{id}/permanent");
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? "用户已彻底删除" : $"删除失败: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"删除失败: {ex.Message}";
        }
        return RedirectToAction("Index", new { includeDeleted = true });
    }

    /// <summary>
    /// 平台角色面板数据（v1.25.0）：该用户现有 API 平台角色 + 可分配角色目录。
    /// 改动说明：平台角色存 API RBAC（业务权限 API 一家管），Identity 角色列仅表认证中心
    /// 身份；浏览器无 API token，故经本控制器代理 by-auth 端点（键=Identity sub）
    /// </summary>
    public async Task<IActionResult> PlatformRoles(string id)
    {
        var client = _factory.CreateClient("ApiClient");
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var roles = new List<string>();
        var catalog = new List<object>();
        var notProvisioned = false;

        var rolesResp = await client.GetAsync($"{apiBase}/api/admin/users/by-auth/{id}/roles");
        if (rolesResp.IsSuccessStatusCode)
        {
            var body = await rolesResp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.Array)
                roles = d.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();
        }
        else if (rolesResp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            notProvisioned = true; // 该用户尚未 JIT 进业务库，无平台角色记录
        }

        var catResp = await client.GetAsync($"{apiBase}/api/admin/roles/all");
        if (catResp.IsSuccessStatusCode)
        {
            var body = await catResp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                catalog = d.EnumerateArray().Select(x => (object)new
                {
                    name = x.GetProperty("name").GetString(),
                    description = x.TryGetProperty("description", out var dd) ? dd.GetString() : null,
                    isSystemRole = x.TryGetProperty("isSystemRole", out var s) && s.GetBoolean()
                }).ToList();
            }
        }

        return Json(new { roles, catalog, notProvisioned });
    }

    /// <summary>
    /// 保存平台角色（v1.25.0）：与现有角色做差集，逐个调 API by-auth 分配/移除。
    /// 自锁守卫：不允许移除自己的 Admin 角色（防后台集体失联）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SavePlatformRoles(string id, List<string> roles)
    {
        roles ??= new List<string>();
        var mySub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.Equals(mySub, id, StringComparison.OrdinalIgnoreCase) && !roles.Contains("Admin"))
            return Json(new { ok = false, message = "不能移除自己的 Admin 角色" });

        var client = _factory.CreateClient("ApiClient");
        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var current = new List<string>();
        var rolesResp = await client.GetAsync($"{apiBase}/api/admin/users/by-auth/{id}/roles");
        if (rolesResp.IsSuccessStatusCode)
        {
            var body = await rolesResp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.Array)
                current = d.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();
        }
        else if (rolesResp.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            return Json(new { ok = false, message = $"读取现有角色失败: {rolesResp.StatusCode}" });
        }
        // 404 = 用户尚未 JIT 进业务库：现有角色视为空集，分配时 API 侧会因无业务用户记录
        // 返回 NotFound——此处先行提示，避免保存时语义不清
        else
        {
            return Json(new { ok = false, message = "该用户尚未登录过业务系统，暂无法分配平台角色" });
        }

        foreach (var add in roles.Except(current))
        {
            var resp = await client.PostAsJsonAsync($"{apiBase}/api/admin/users/by-auth/{id}/roles", new { roleName = add });
            if (!resp.IsSuccessStatusCode)
                return Json(new { ok = false, message = $"分配 {add} 失败: {resp.StatusCode}" });
        }

        foreach (var remove in current.Except(roles))
        {
            var resp = await client.DeleteAsync($"{apiBase}/api/admin/users/by-auth/{id}/roles/{Uri.EscapeDataString(remove)}");
            if (!resp.IsSuccessStatusCode)
                return Json(new { ok = false, message = $"移除 {remove} 失败: {resp.StatusCode}" });
        }

        return Json(new { ok = true, message = "平台角色已保存" });
    }
}
