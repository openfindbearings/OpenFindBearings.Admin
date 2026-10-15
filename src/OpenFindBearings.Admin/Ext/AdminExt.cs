namespace OpenFindBearings.Admin.Ext;

/// <summary>
/// 健康检查端点描述（v2.18.0 通用扩展点）：扩展模块追加检查项时给出 URL/HttpClient 名/探测路径。
/// </summary>
public record ServiceEndpoint(string Url, string Client, string Path);

/// <summary>
/// 后台侧栏扩展菜单项（v2.18.0 通用扩展点）：模块声明一条菜单。
/// 分组语义：Group="审核" 并入开源"审核"分组；Group=null/空 追加为顶层项；
/// Group=其它键 渲染为一个模块声明的新分组（has-sub），组名/图标取该组首项的 GroupLabel/GroupIcon。
/// </summary>
/// <param name="Group">分组键：空=顶层项；"审核"=并入审核分组；其它=模块自建分组键</param>
/// <param name="Label">菜单文字</param>
/// <param name="Controller">MVC 控制器</param>
/// <param name="Action">MVC action</param>
/// <param name="Permission">所需面板权限（为空则不判权限）</param>
/// <param name="Icon">FontAwesome 图标类（默认圆点）</param>
/// <param name="GroupLabel">模块自建分组的显示名（该组首项生效；为空用分组键）</param>
/// <param name="GroupIcon">模块自建分组的图标类（默认文件夹）</param>
public record AdminMenuItem(
    string Group,
    string Label,
    string Controller,
    string Action,
    string? Permission = null,
    string Icon = "fa-circle",
    string? GroupLabel = null,
    string? GroupIcon = "fa-folder");

/// <summary>
/// 后台扩展模块接口（v2.18.0 通用扩展点）。
/// 开源基座只识别"模块"这一通用概念、不含任何具体业务；闭源在同项目内新增实现类即可被
/// <see cref="AdminModuleLoader"/> 自动发现，并把路由/健康检查/侧栏菜单接进既有骨架。
/// 目的：闭源只做加法（新增 .Pro.cs 文件），开源不再为闭源预留 互联网数据专属占位，双方永不改同一共享文件。
/// </summary>
public interface IAdminModule
{
    /// <summary>
    /// 模块是否启用：读取本模块自己的配置键（如模块开关）；关闭则整体不接入。
    /// </summary>
    /// <param name="config">应用配置</param>
    bool IsEnabled(IConfiguration config);

    /// <summary>
    /// 注册本模块所需的服务（如专属命名 HttpClient）；在 Build 前调用。
    /// </summary>
    /// <param name="builder">应用构造器（可注册 HttpClient/服务）</param>
    void ConfigureServices(WebApplicationBuilder builder);

    /// <summary>
    /// 映射本模块的专属路由（如代理到闭源 互联网数据服务的 /api/proxy/*）。无则空实现。
    /// </summary>
    /// <param name="app">已构建的 WebApplication（可经 app.Services 取依赖）</param>
    void MapRoutes(WebApplication app);

    /// <summary>
    /// 追加本模块的服务健康检查项（写入 name → 端点 映射）。
    /// </summary>
    /// <param name="config">应用配置</param>
    /// <param name="map">健康检查项映射（就地追加）</param>
    void AddHealthServices(IConfiguration config, IDictionary<string, ServiceEndpoint> map);

    /// <summary>
    /// 本模块贡献的侧栏菜单项（可为空集合）。
    /// </summary>
    IEnumerable<AdminMenuItem> MenuItems { get; }

    /// <summary>
    /// 富化仪表盘统计 JSON（可选）：传入 API 原始 JSON，返回富化后 JSON；返回 null 表示不修改。
    /// 闭源可借此把 互联网数据审核待办等并入仪表盘，开源无模块 = 原样返回。
    /// </summary>
    /// <param name="services">应用服务提供器（模块可自取 HttpClientFactory/配置）</param>
    /// <param name="apiJson">API 返回的原始仪表盘 JSON</param>
    Task<string?> EnrichDashboardJsonAsync(IServiceProvider services, string apiJson);

    /// <summary>
    /// 加载本模块提供的审计日志源（可选）：source 命中本模块时返回该源原始 JSON（交由骨架按统一口径解析）；
    /// 不处理返回 null。骨架只内置 identity/api，其它源（如闭源服务）由模块贡献。开源基座默认 null。
    /// </summary>
    /// <param name="services">应用服务提供器（模块可自取 HttpClientFactory/配置）</param>
    /// <param name="source">审计源标识</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页条数</param>
    Task<string?> LoadAuditLogJsonAsync(IServiceProvider services, string source, int page, int pageSize) => Task.FromResult<string?>(null);
}