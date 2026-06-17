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
        Add("admin", "DashboardView");
        Add("admin", "BearingView");
        Add("admin", "BearingCreate");
        Add("admin", "BearingEdit");
        Add("admin", "BearingDelete");
        Add("admin", "MerchantView");
        Add("admin", "MerchantManage");
        Add("admin", "MerchantVerify");
        Add("admin", "CorrectionReview");
        Add("admin", "EtlManage");
        Add("admin", "CrawlerManage");
        Add("admin", "RoleManage");
        Add("admin", "UserManage");
        Add("admin", "SystemView");
        Add("admin", "SystemManage");
        Add("admin", "AuditView");
        Add("admin", "DataRestore");

        // editor（业务员）角色：日常 CRUD + 审核
        Add("editor", "DashboardView");
        Add("editor", "BearingView");
        Add("editor", "BearingCreate");
        Add("editor", "BearingEdit");
        Add("editor", "BearingDelete");
        Add("editor", "MerchantView");
        Add("editor", "MerchantManage");
        Add("editor", "CorrectionReview");
        Add("editor", "SystemView");

        // viewer（审计员）角色：只读
        Add("viewer", "DashboardView");
        Add("viewer", "BearingView");
        Add("viewer", "MerchantView");
        Add("viewer", "SystemView");
        Add("viewer", "AuditView");

        db.AdminRolePermissions.AddRange(permissions);
        db.SaveChanges();
    }
}
