namespace OpenFindBearings.Admin.Authorization;

/// <summary>
/// 面板运营角色全集（v2.13.2 收敛）：登录态经 API RBAC 拉取，凡不属此集合者拒入后台面板；
/// 此前同一集合以字面量散落在 Program.cs 登录校验、_Layout 菜单短路与 Users 页角色列三处。
/// </summary>
public static class PanelRoles
{
    /// <summary>超管（panel_role=Admin 时菜单全显示，与 Controller 的 PanelPermission 策略同口径）</summary>
    public const string Admin = "Admin";

    /// <summary>运营</summary>
    public const string Operator = "Operator";

    /// <summary>审核</summary>
    public const string Auditor = "Auditor";

    /// <summary>面板角色全集（登录准入与角色列共用）</summary>
    public static readonly string[] All = [Admin, Operator, Auditor];
}