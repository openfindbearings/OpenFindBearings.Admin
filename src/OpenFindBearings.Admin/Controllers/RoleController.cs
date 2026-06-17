using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Data;
using OpenFindBearings.Admin.Models.Entities;
using OpenFindBearings.Admin.Models.ViewModels;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 角色管理控制器（Admin 本地 RBAC，db_admin 数据库）
/// </summary>
[Authorize]
public class RoleController : Controller
{
    private readonly ApplicationDbContext _db;

    public RoleController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 角色列表
    /// </summary>
    public IActionResult Index()
    {
        var roles = _db.AdminRolePermissions
            .GroupBy(r => r.RoleName)
            .Select(g => new RoleViewModel
            {
                RoleName = g.Key,
                PermissionCount = g.Count(p => p.Granted)
            })
            .OrderBy(r => r.RoleName)
            .ToList();
        return View(roles);
    }

    /// <summary>
    /// 角色权限详情
    /// </summary>
    public IActionResult Permissions(string roleName)
    {
        if (string.IsNullOrEmpty(roleName))
            return RedirectToAction("Index");

        var permissions = _db.AdminRolePermissions
            .Where(p => p.RoleName == roleName)
            .ToList();

        ViewBag.RoleName = roleName;

        var allPermissions = Enum.GetValues<Models.Enums.PermissionKey>()
            .Select(p => new PermissionItemViewModel
            {
                Key = p.ToString(),
                Granted = permissions.Any(x => x.PermissionKey == p.ToString() && x.Granted)
            })
            .ToList();

        return View(allPermissions);
    }

    /// <summary>
    /// 创建角色
    /// </summary>
    [HttpPost]
    public IActionResult Create(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            TempData["Error"] = "角色名不能为空";
            return RedirectToAction("Index");
        }

        roleName = roleName.Trim();
        if (_db.AdminRolePermissions.Any(r => r.RoleName == roleName))
        {
            TempData["Error"] = $"角色 '{roleName}' 已存在";
            return RedirectToAction("Index");
        }

        var now = DateTime.UtcNow;
        var permissions = Enum.GetValues<Models.Enums.PermissionKey>()
            .Select(p => new AdminRolePermission
            {
                Id = Guid.NewGuid(),
                RoleName = roleName,
                PermissionKey = p.ToString(),
                Granted = false,
                CreatedAt = now
            })
            .ToList();

        _db.AdminRolePermissions.AddRange(permissions);
        _db.SaveChanges();

        TempData["Success"] = $"角色 '{roleName}' 创建成功";
        return RedirectToAction("Permissions", new { roleName });
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    [HttpPost]
    public IActionResult Delete(string roleName)
    {
        if (string.IsNullOrEmpty(roleName))
            return RedirectToAction("Index");

        var permissions = _db.AdminRolePermissions.Where(r => r.RoleName == roleName).ToList();
        _db.AdminRolePermissions.RemoveRange(permissions);
        _db.SaveChanges();

        TempData["Success"] = $"角色 '{roleName}' 已删除";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 保存角色权限
    /// </summary>
    [HttpPost]
    public IActionResult SavePermissions(string roleName, List<string> grantedPermissions)
    {
        if (string.IsNullOrEmpty(roleName))
            return RedirectToAction("Index");

        var existing = _db.AdminRolePermissions.Where(r => r.RoleName == roleName).ToList();
        _db.AdminRolePermissions.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var allPerms = Enum.GetValues<Models.Enums.PermissionKey>()
            .Select(p => new AdminRolePermission
            {
                Id = Guid.NewGuid(),
                RoleName = roleName,
                PermissionKey = p.ToString(),
                Granted = grantedPermissions.Contains(p.ToString()),
                CreatedAt = now
            })
            .ToList();

        _db.AdminRolePermissions.AddRange(allPerms);
        _db.SaveChanges();

        TempData["Success"] = $"角色 '{roleName}' 权限已更新";
        return RedirectToAction("Permissions", new { roleName });
    }
}
