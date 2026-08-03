namespace OpenFindBearings.Admin.Services;

public class ServiceHealthService
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public ServiceHealthService(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task<Dictionary<string, ServiceStatus>> CheckAllAsync()
    {
        var result = new Dictionary<string, ServiceStatus>();

        var services = new Dictionary<string, (string url, string client)>
        {
            ["Api"] = (_config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183", "ApiClient"),
            ["Sync"] = (_config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206", "SyncClient"),
            ["Identity"] = (_config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201", "IdentityClient")
        };

        var paths = new Dictionary<string, string>
        {
            ["Api"] = "/health/live",
            ["Sync"] = "/health/live",
            ["Identity"] = "/health/live"
        };

        foreach (var (name, (baseUrl, clientName)) in services)
        {
            try
            {
                var client = _factory.CreateClient(clientName);
                var resp = await client.GetAsync($"{baseUrl}{paths[name]}");
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

public class ServiceStatus
{
    public bool Available { get; set; }
    public string Message { get; set; } = string.Empty;
}
