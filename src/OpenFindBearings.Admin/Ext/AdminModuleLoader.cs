using System.Reflection;

namespace OpenFindBearings.Admin.Ext;

/// <summary>
/// 扩展模块加载器（v2.18.0 通用扩展点）：反射发现当前 Admin 程序集中所有 <see cref="IAdminModule"/>
/// 实现并按 IsEnabled 过滤。开源基座内无任何实现 = 返回空列表（零副作用）；闭源在同项目新增实现类即被接入。
/// </summary>
public static class AdminModuleLoader
{
    /// <summary>
    /// 发现并实例化所有启用的扩展模块（无实现=空列表）。
    /// </summary>
    /// <param name="config">应用配置（用于各模块的 IsEnabled 判定）</param>
    /// <returns>已启用模块列表</returns>
    public static IReadOnlyList<IAdminModule> Discover(IConfiguration config)
    {
        var modules = new List<IAdminModule>();
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !typeof(IAdminModule).IsAssignableFrom(type))
                continue;

            if (Activator.CreateInstance(type) is IAdminModule module && module.IsEnabled(config))
                modules.Add(module);
        }
        return modules;
    }
}