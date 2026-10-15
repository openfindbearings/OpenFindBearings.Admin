using OpenFindBearings.Admin.Ext;
using OpenFindBearings.Admin.Helpers;

namespace OpenFindBearings.Admin.Services;

/// <summary>
/// 服务健康检查（v2.18.0 通用扩展点改造）：内置 Api/Identity 检查项，经 IAdminModule 追加闭源专属项
/// （如闭源服务）；开源基座无模块 = 仅内置两项。
/// </summary>
public class ServiceHealthService
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly IReadOnlyList<IAdminModule> _modules;

    /// <summary>构造健康检查服务，注入 HttpClient 工厂、配置与扩展模块</summary>
    public ServiceHealthService(IHttpClientFactory factory, IConfiguration config, IReadOnlyList<IAdminModule> modules)
    {
        _factory = factory;
        _config = config;
        _modules = modules;
    }

    /// <summary>检查全部服务（内置项 + 扩展模块追加项），返回 name → 状态</summary>
    public async Task<Dictionary<string, ServiceStatus>> CheckAllAsync()
    {
        var result = new Dictionary<string, ServiceStatus>();

        // 内置检查项（开源基座固定两项）
        var services = new Dictionary<string, ServiceEndpoint>
        {
            ["Api"] = new(ApiUrlResolver.Api(_config), "ApiClient", "/health/live"),
            ["Identity"] = new(ApiUrlResolver.Identity(_config), "IdentityClient", "/health/live")
        };

        // 改动说明（v2.18.0 通用扩展点）：闭源模块追加专属检查项（如闭源服务），开源无模块 = 无
        foreach (var module in _modules)
            module.AddHealthServices(_config, services);

        foreach (var (name, ep) in services)
        {
            try
            {
                var client = _factory.CreateClient(ep.Client);
                var resp = await client.GetAsync($"{ep.Url}{ep.Path}");
                result[name] = new ServiceStatus { Available = resp.IsSuccessStatusCode, Message = resp.StatusCode.ToString() };
            }
            catch (Exception ex)
            {
                result[name] = new ServiceStatus { Available = false, Message = ex.Message };
            }
        }
        return result;
    }
}

/// <summary>单个服务的健康状态</summary>
public class ServiceStatus
{
    /// <summary>是否可用</summary>
    public bool Available { get; set; }

    /// <summary>状态说明（HTTP 状态码或异常信息）</summary>
    public string Message { get; set; } = string.Empty;
}