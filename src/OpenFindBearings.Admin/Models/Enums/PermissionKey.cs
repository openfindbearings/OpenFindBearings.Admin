namespace OpenFindBearings.Admin.Models.Constants;

public static class PermissionKeys
{
    public const string DashboardView = "dashboard.view";
    public const string BearingView = "bearing.view";
    public const string BearingCreate = "bearing.create";
    public const string BearingEdit = "bearing.edit";
    public const string BearingDelete = "bearing.delete";
    public const string MerchantView = "merchant.view";
    public const string MerchantManage = "merchant.manage";
    public const string MerchantVerify = "merchant.verify";
    public const string CorrectionReview = "correction.review";
    public const string EtlManage = "etl.manage";
    public const string CrawlerManage = "crawler.manage";
    public const string RoleManage = "role.manage";
    public const string UserManage = "user.manage";
    public const string SystemView = "system.view";
    public const string SystemManage = "system.manage";
    public const string AuditView = "audit.view";
    public const string DataRestore = "data.restore";
    public const string DataHardDelete = "data.harddelete";

    public static IReadOnlyList<string> All => new[]
    {
        DashboardView, BearingView, BearingCreate, BearingEdit, BearingDelete,
        MerchantView, MerchantManage, MerchantVerify, CorrectionReview,
        EtlManage, CrawlerManage, RoleManage, UserManage,
        SystemView, SystemManage, AuditView, DataRestore, DataHardDelete
    };
}
