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

        var services = new Dictionary<string, string>
        {
            ["Api"] = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183",
            ["Crawler"] = _config["ApiUrls:FindBearingsCrawler"] ?? "https://localhost:7207",
            ["Sync"] = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206",
            ["Identity"] = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201"
        };

        foreach (var (name, baseUrl) in services)
        {
            try
            {
                var client = name switch
                {
                    "Api" => _factory.CreateClient("ApiClient"),
                    "Crawler" => _factory.CreateClient("CrawlerClient"),
                    "Sync" => _factory.CreateClient("SyncClient"),
                    "Identity" => _factory.CreateClient("IdentityClient"),
                    _ => _factory.CreateClient("ApiClient")
                };
                var resp = await client.GetAsync($"{baseUrl}/live");
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
