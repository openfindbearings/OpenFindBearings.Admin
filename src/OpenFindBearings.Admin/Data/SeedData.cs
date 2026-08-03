using OpenFindBearings.Admin.Models.Entities;

namespace OpenFindBearings.Admin.Data;

public static class SeedData
{
    public static void Initialize(ApplicationDbContext db)
    {
        if (db.AdminRolePermissions.Any())
            return;

        var permissions = new List<AdminRolePermission>();
        var now = DateTime.UtcNow;

        void Add(string role, string permission)
        {
            permissions.Add(new AdminRolePermission
            {
                Id = Guid.NewGuid(),
                RoleName = role,
                PermissionKey = permission,
                Granted = true,
                CreatedAt = now
            });
        }

        // admin 角色：全部权限
        Add("admin", "dashboard.view");
        Add("admin", "bearing.view");
        Add("admin", "bearing.create");
        Add("admin", "bearing.edit");
        Add("admin", "bearing.delete");
        Add("admin", "merchant.view");
        Add("admin", "merchant.manage");
        Add("admin", "merchant.verify");
        Add("admin", "correction.review");
        Add("admin", "etl.manage");
        Add("admin", "role.manage");
        Add("admin", "user.manage");
        Add("admin", "system.view");
        Add("admin", "system.manage");
        Add("admin", "audit.view");
        Add("admin", "data.restore");
        Add("admin", "data.harddelete");

        // editor（业务员）角色：日常 CRUD + 审核
        Add("editor", "dashboard.view");
        Add("editor", "bearing.view");
        Add("editor", "bearing.create");
        Add("editor", "bearing.edit");
        Add("editor", "bearing.delete");
        Add("editor", "merchant.view");
        Add("editor", "merchant.manage");
        Add("editor", "correction.review");
        Add("editor", "system.view");

        // viewer（观察员）角色：基本查询
        Add("viewer", "dashboard.view");
        Add("viewer", "bearing.view");
        Add("viewer", "merchant.view");
        Add("viewer", "system.view");
        Add("viewer", "audit.view");

        db.AdminRolePermissions.AddRange(permissions);
        db.SaveChanges();
    }
}
