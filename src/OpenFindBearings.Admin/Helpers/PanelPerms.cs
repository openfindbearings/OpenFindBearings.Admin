using System.Security.Claims;

namespace OpenFindBearings.Admin.Helpers;

/// <summary>
/// 视图侧权限判定 helper（v1.30.0 权限目录重排）：
/// 子视图按钮级显隐统一走 PanelPerms.Has(User, "键")，语义与 PanelPermission 策略同口径——
/// panel_role=Admin 短路全通过，否则 permission 多值 claim 任一命中
/// </summary>
public static class PanelPerms
{
    /// <summary>
    /// 判断当前登录用户是否具备任一给定权限点（超管短路）
    /// </summary>
    public static bool Has(ClaimsPrincipal user, params string[] perms)
    {
        if (user.HasClaim(c => c.Type == "panel_role" && c.Value == "Admin")) return true;
        var set = user.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToHashSet();
        return perms.Any(p => set.Contains(p));
    }
}
