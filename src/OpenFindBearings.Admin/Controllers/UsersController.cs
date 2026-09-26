using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 用户管理控制器（调用 Identity API）
/// </summary>
[Authorize]
[PanelPermission("user.view")]
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
    /// 改动说明（v1.30.0）：includeDeleted 勾选升级为状态下拉（正常/已禁用=黑名单/已锁定/已删除/全部）——
    /// 封禁处置与软删待恢复是两种生命周期阶段，各给独立视图快速定位
    /// </summary>
    public async Task<IActionResult> Index(string search = "", int page = 1, int pageSize = 20, string status = "enabled", string tab = "all")
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        var url = $"{identityBase}/api/account/admin/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        // 状态映射：deleted 走新枚举但须带 includeDeleted 才能看到软删行；all=活跃+禁用+软删全量
        var effectiveStatus = string.IsNullOrWhiteSpace(status) ? "enabled" : status.Trim().ToLowerInvariant();
        if (effectiveStatus == "deleted")
        {
            url += "&status=deleted&includeDeleted=true";
        }
        else if (effectiveStatus == "all")
        {
            url += "&includeDeleted=true";
        }
        else
        {
            url += $"&status={Uri.EscapeDataString(effectiveStatus)}";
        }
        ViewBag.Status = effectiveStatus;

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
        // 改动说明（v1.30.0）：IncludeDeleted 已废弃，状态下拉以 ViewBag.Status 传递（前段已赋值）
        ViewBag.Tab = tab;

        // 改动说明（v1.29.0）：Identity 列表的 Roles 是认证中心角色（恒空，业务平台角色不在 Identity），
        // 角色列改从 API 批量端点 /api/admin/users/platform-roles 拉 sub→roles[] 字典合并渲染；
        // 拉取失败降级为"无角色显示"，不阻塞列表主流程
        try
        {
            var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
            var apiClient = _factory.CreateClient("ApiClient");
            var prResp = await apiClient.GetAsync($"{apiBase}/api/admin/users/platform-roles");
            if (prResp.IsSuccessStatusCode)
            {
                var prJson = await prResp.Content.ReadAsStringAsync();
                var prDoc = System.Text.Json.JsonDocument.Parse(prJson);
                var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                if (prDoc.RootElement.TryGetProperty("data", out var dd) && dd.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in dd.EnumerateObject())
                        map[prop.Name] = prop.Value.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x != "").ToList();
                }
                ViewBag.PlatformRoles = map;
            }
            else
            {
                ViewBag.PlatformRoles = new Dictionary<string, List<string>>();
            }

            // 改动说明（v1.29.0）：角色徽章显示中文名——拉 API 角色目录建 name→displayName 映射
            // （roles/all 现有端点，角色数量级小；失败降级为显示英文标识）
            var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var roleCatalog = new List<(string Name, string? Display)>();
            var rdResp = await apiClient.GetAsync($"{apiBase}/api/admin/roles/all");
            if (rdResp.IsSuccessStatusCode)
            {
                var rdJson = await rdResp.Content.ReadAsStringAsync();
                using var rdDoc = System.Text.Json.JsonDocument.Parse(rdJson);
                if (rdDoc.RootElement.TryGetProperty("data", out var rarr) && rarr.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var r in rarr.EnumerateArray())
                    {
                        var nm = r.TryGetProperty("name", out var n) ? n.GetString() : null;
                        var dn = r.TryGetProperty("displayName", out var d2) && d2.ValueKind == System.Text.Json.JsonValueKind.String ? d2.GetString() : null;
                        if (!string.IsNullOrEmpty(nm))
                        {
                            if (!string.IsNullOrEmpty(dn)) displayNames[nm] = dn!;
                            roleCatalog.Add((nm!, dn));
                        }
                    }
                }
            }
            ViewBag.RoleDisplayNames = displayNames;
            // 新建用户弹窗的角色复选目录（v1.29.0 建号即分配）
            ViewBag.RoleCatalog = roleCatalog;
        }
        catch
        {
            ViewBag.PlatformRoles = new Dictionary<string, List<string>>();
            ViewBag.RoleDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ViewBag.RoleCatalog = new List<(string Name, string? Display)>();
        }

        return View();
    }

    /// <summary>
    /// 创建后台用户（Identity 建号 + API 预置挂角色）
    /// 改动说明（v1.29.0）：入口语义定死为"后台用户"——平台角色至少勾一个
    /// （不勾建出来是进不了任何面板的孤儿账号，App 用户走 app 自助注册）；
    /// 建号成功即调 API provision 预置业务行并授权，解"未登录过不能分配角色"死结；
    /// provision 失败不回滚账号，提示稍后手动补
    /// </summary>
    [HttpPost]
    [PanelPermission("user.manage")]
    public async Task<IActionResult> Create(string userName, string? email, string? name, string? phoneNumber, List<string>? roles, string? tab, string? status)
    {
        if (roles == null || roles.Count == 0)
        {
            TempData["Error"] = "后台用户必须至少分配一个平台角色（注册用户请在 app 端自助注册）";
            return RedirectToAction("Index", new { tab, status });
        }

        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:5001";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var payload = new
            {
                userName,
                email,
                name,
                phoneNumber
            };
            var resp = await client.PostAsJsonAsync($"{identityBase}/api/account/admin/users", payload);
            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = $"创建失败: HTTP {(int)resp.StatusCode}";
                return RedirectToAction("Index", new { tab, status });
            }

            // 解析 Identity 返回的新用户 id（data.id），作为 API provision 的 authUserId
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var newId = doc.RootElement.TryGetProperty("data", out var d) && d.TryGetProperty("id", out var i) ? i.GetString() : null;
            if (!string.IsNullOrEmpty(newId))
            {
                var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
                var apiClient = _factory.CreateClient("ApiClient");
                var pr = await apiClient.PostAsJsonAsync($"{apiBase}/api/admin/users/provision",
                    new { authUserId = newId, userName, roles });
                TempData["Success"] = pr.IsSuccessStatusCode
                    ? $"用户 '{userName}' 创建成功并已分配角色"
                    : $"用户 '{userName}' 已创建，但平台角色预置失败（{pr.StatusCode}），请稍后在\"平台角色\"中手动分配";
            }
            else
            {
                TempData["Success"] = $"用户 '{userName}' 已创建，但未取到新账号 ID，请手动分配平台角色";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"创建失败: {ex.Message}";
        }
        return RedirectToAction("Index", new { tab, status });
    }

    /// <summary>
    /// 启用/禁用用户
    /// 改动说明（v1.29.2）：① 原 PatchAsync 传 null body，Identity 端点 [FromBody] 必 415
    /// UnsupportedMediaType（禁用/启用一直全坏），改传 {enable:当前取反} 显式目标态；
    /// ② 回跳保留 tab 参数，操作后不跳回默认页签
    /// </summary>
    [HttpPost]
    [PanelPermission("user.ban")]
    // 改动说明（v1.31.1）：status 透传——操作回跳停在当前状态视图（黑名单里解禁不弹回正常页）
    public async Task<IActionResult> ToggleStatus(string id, bool enable, string? tab, string? status)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var resp = await client.PatchAsync($"{identityBase}/api/account/admin/users/{id}/status",
                new StringContent(System.Text.Json.JsonSerializer.Serialize(new { enable }), System.Text.Encoding.UTF8, "application/json"));
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode ? (enable ? "已启用，用户回到「正常」视图" : "已禁用（黑名单），该用户将从「正常」视图消失，可在状态筛选「已禁用」中找到并删除/恢复") : $"操作失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index", new { tab, status });
    }

    /// <summary>
    /// 解锁用户
    /// </summary>
    [HttpPost]
    [PanelPermission("user.ban")]
    public async Task<IActionResult> Unlock(string id, string? tab, string? status)
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
        return RedirectToAction("Index", new { tab, status });
    }

    /// <summary>
    /// <summary>
    /// 重置密码为系统初始密码（v1.30.0：不再人工输入新密码——调 Identity reset-to-default，
    /// 密码值单一事实源在 Identity 配置；成功后回显初始密码供转告本人，该账号首登被强制改密）
    /// </summary>
    [HttpPost]
    [PanelPermission("user.ban")]
    public async Task<IActionResult> ResetPassword(string id, string? tab, string? status)
    {
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var resp = await client.PostAsync($"{identityBase}/api/account/admin/users/{id}/reset-to-default", null);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                var pwd = doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String ? d.GetString() : null;
                TempData["Success"] = string.IsNullOrEmpty(pwd) ? "已重置为初始密码" : $"已重置为初始密码：{pwd}（请转告本人，首次登录须修改）";
            }
            else
            {
                TempData["Error"] = $"重置失败: {resp.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"操作失败: {ex.Message}";
        }
        return RedirectToAction("Index", new { tab, status });
    }

    /// <summary>
    /// 恢复已删除用户
    /// </summary>
    [HttpPost]
    [PanelPermission("user.manage")]
    public async Task<IActionResult> Restore(string id, string? tab, string? status)
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
        return RedirectToAction("Index", new { tab, status });
    }

    /// <summary>
    /// 彻底删除用户（物理删除，不可恢复）
    /// </summary>
    [HttpPost]
    [PanelPermission("data.harddelete")]
    public async Task<IActionResult> HardDelete(string id, string? tab, string? status)
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
        return RedirectToAction("Index", new { status = "deleted" });
    }

    /// <summary>
    /// 软删除用户（v1.31.1 两步链路：正常→禁用→删除，删除按钮仅出现在已禁用行；
    /// 已删除视图可恢复或彻底删除）
    /// 守卫（fail-closed，删除属危险操作，验证不了就拒）：不能删自己；
    /// 目标是 Admin 且无其他启用 Admin 时拒绝（防后台集体锁死）。
    /// 注意：删除 app 用户不会级联清理其商户成员/积分/收藏等业务数据——
    /// 常规注销应引导用户走 app 内注销（30 天冷静期+匿名化链），本删除是后台显权
    /// </summary>
    [HttpPost]
    [PanelPermission("user.manage")]
    public async Task<IActionResult> Delete(string id, string? tab, string? status)
    {
        var mySub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(mySub) && mySub == id)
        {
            TempData["Error"] = "不能删除自己的账号";
            return RedirectToAction("Index", new { tab, status });
        }

        var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
        var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
        try
        {
            // 最后启用管理员守卫：目标挂 Admin 角色时，须存在其他启用 Admin
            var apiClient = _factory.CreateClient("ApiClient");
            var mapResp = await apiClient.GetAsync($"{apiBase}/api/admin/users/platform-roles");
            if (!mapResp.IsSuccessStatusCode)
            {
                TempData["Error"] = "无法校验角色信息（API 不可达），删除已拒绝，请稍后重试";
                return RedirectToAction("Index", new { tab, status });
            }
            using var mapDoc = System.Text.Json.JsonDocument.Parse(await mapResp.Content.ReadAsStringAsync());
            var roleMap = mapDoc.RootElement.TryGetProperty("data", out var dd) ? dd : default;
            var targetIsAdmin = roleMap.ValueKind == System.Text.Json.JsonValueKind.Object
                && roleMap.TryGetProperty(id, out var tr)
                && tr.EnumerateArray().Any(x => x.GetString() == "Admin");
            if (targetIsAdmin)
            {
                var idClient = _factory.CreateClient("IdentityClient");
                var listResp = await idClient.GetAsync($"{identityBase}/api/account/admin/users?page=1&pageSize=100&status=enabled");
                if (!listResp.IsSuccessStatusCode)
                {
                    TempData["Error"] = "无法统计启用管理员（Identity 不可达），删除已拒绝，请稍后重试";
                    return RedirectToAction("Index", new { tab, status });
                }
                using var listDoc = System.Text.Json.JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
                var otherActiveAdmins = 0;
                if (listDoc.RootElement.TryGetProperty("data", out var ld)
                    && ld.TryGetProperty("items", out var items)
                    && roleMap.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var u in items.EnumerateArray())
                    {
                        var uid = u.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(uid) || uid.Equals(id, StringComparison.OrdinalIgnoreCase)) continue;
                        if (roleMap.TryGetProperty(uid, out var rs)
                            && rs.EnumerateArray().Any(x => x.GetString() == "Admin")) otherActiveAdmins++;
                    }
                }
                if (otherActiveAdmins == 0)
                {
                    TempData["Error"] = "不能删除：该账号是唯一启用的管理员（防后台锁死，请先为其他账号分配管理员角色）";
                    return RedirectToAction("Index", new { tab, status });
                }
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"删除前校验失败，已拒绝: {ex.Message}";
            return RedirectToAction("Index", new { tab, status });
        }

        var client = _factory.CreateClient("IdentityClient");
        try
        {
            var resp = await client.DeleteAsync($"{identityBase}/api/account/admin/users/{id}");
            TempData[resp.IsSuccessStatusCode ? "Success" : "Error"] = resp.IsSuccessStatusCode
                ? "用户已删除（可在「已删除」筛选中恢复或彻底删除）"
                : $"删除失败: {resp.StatusCode}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"删除失败: {ex.Message}";
        }
        return RedirectToAction("Index", new { tab, status });
    }

    /// <summary>
    /// 平台角色面板数据（v1.25.0）：该用户现有 API 平台角色 + 可分配角色目录。
    /// 改动说明：平台角色存 API RBAC（业务权限 API 一家管），Identity 角色列仅表认证中心
    /// 身份；浏览器无 API token，故经本控制器代理 by-auth 端点（键=Identity sub）
    /// </summary>
    [PanelPermission("user.assign")]
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
                    // 改动说明（v1.29.0）：目录带中文显示名，弹窗勾选行主显 DisplayName
                    displayName = x.TryGetProperty("displayName", out var dn) && dn.ValueKind == System.Text.Json.JsonValueKind.String ? dn.GetString() : null,
                    description = x.TryGetProperty("description", out var dd) && dd.ValueKind == System.Text.Json.JsonValueKind.String ? dd.GetString() : null,
                    isSystemRole = x.TryGetProperty("isSystemRole", out var isSr) && isSr.GetBoolean()
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
    [PanelPermission("user.assign")]
    public async Task<IActionResult> SavePlatformRoles(string id, List<string> roles, string? userName)
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
        // 改动说明（v1.29.2）：原"404 才走 provision"判断失效——API GET by-auth 对无业务行用户
        // 返回 200+空数组而非 404，保存时拿空 current 去逐个 POST 授权撞 404（截图"分配 Operator 失败"）。
        // 改为无条件先 provision（幂等：行存在只补角色，不存在则建行+挂全部勾选角色），
        // 再移除"原有但本次未勾"的角色；勾选态=最终态语义不变
        var prResp = await client.PostAsJsonAsync($"{apiBase}/api/admin/users/provision",
            new { authUserId = id, userName, roles });
        if (!prResp.IsSuccessStatusCode)
        {
            return Json(new { ok = false, message = $"角色预置失败: {prResp.StatusCode}（确认 API 版本含 provision 端点）" });
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
