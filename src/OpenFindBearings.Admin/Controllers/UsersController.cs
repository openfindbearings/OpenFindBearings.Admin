using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 用户管理控制器（调用 Identity API）
/// </summary>
[Authorize]
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

        try
        {
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<ApiResponseWrapper<PaginatedWrapper<UserItemDto>>>(json);
                ViewBag.Items = result?.Data?.Items ?? [];
                ViewBag.TotalCount = result?.Data?.TotalCount ?? 0;
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
            }
        }
        catch (Exception ex)
        {
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

    #region 内部类型

    private class ApiResponseWrapper<T>
    {
        public T? Data { get; set; }
    }

    private class PaginatedWrapper<T>
    {
        public List<T> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
    }

    #endregion
}
