using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace OpenFindBearings.Admin.Authorization;

/// <summary>
/// 面板权限要求（v1.25.0）：与 cookie 中 permission 多值 claim 任一命中即通过；
/// panel_role=Admin 短路全通过（Admin 角色语义=全部权限，避免权限清单漏配锁死超管）
/// </summary>
public class PanelPermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// 构造：任一命中即通过的权限点清单
    /// </summary>
    public PanelPermissionRequirement(params string[] permissions) => Permissions = permissions;

    /// <summary>
    /// 权限点清单（OR 语义）
    /// </summary>
    public IReadOnlyCollection<string> Permissions { get; }
}

/// <summary>
/// 面板权限处理器（v1.25.0）：读登录时写入的 permission/panel_role claim，零跨服务调用
/// </summary>
public class PanelPermissionHandler : AuthorizationHandler<PanelPermissionRequirement>
{
    /// <summary>
    /// 命中判定：Admin 面板角色短路，否则权限点任一命中
    /// </summary>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PanelPermissionRequirement requirement)
    {
        var permissions = context.User.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToHashSet();
        var isPanelAdmin = context.User.HasClaim(c => c.Type == "panel_role" && c.Value == "Admin");
        if (isPanelAdmin || requirement.Permissions.Any(p => permissions.Contains(p)))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

/// <summary>
/// 面板权限特性（v1.25.0）：[PanelPermission("user.manage")] 语法糖，
/// 策略名 Panel:xxx 由 PanelPolicyProvider 按需动态生成，无需启动期逐个注册
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class PanelPermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// 构造：权限点 OR 列表转策略名
    /// </summary>
    public PanelPermissionAttribute(params string[] permissions)
        => Policy = PanelPolicyProvider.PolicyPrefix + string.Join(",", permissions);
}

/// <summary>
/// 动态策略提供器（v1.25.0）：Panel:a,b 形式策略名按需构建（Cookie 方案 + PanelPermissionRequirement），
/// 其余策略名回退默认提供器
/// </summary>
public class PanelPolicyProvider : IAuthorizationPolicyProvider
{
    /// <summary>
    /// 动态策略名前缀
    /// </summary>
    public const string PolicyPrefix = "Panel:";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    /// <summary>
    /// 构造：内嵌默认提供器作回退
    /// </summary>
    public PanelPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    /// <inheritdoc/>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    /// <inheritdoc/>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    /// <inheritdoc/>
    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permissions = policyName[PolicyPrefix.Length..].Split(',', StringSplitOptions.RemoveEmptyEntries);
            return new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new PanelPermissionRequirement(permissions))
                .Build();
        }

        return await _fallback.GetPolicyAsync(policyName);
    }
}
