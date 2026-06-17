using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace OpenFindBearings.Admin.Services
{
    /// <summary>
    /// 将当前用户的 JWT 访问令牌注入到所有后端 API 请求中
    /// </summary>
    public class BearerTokenHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<BearerTokenHandler> _logger;

        public BearerTokenHandler(IHttpContextAccessor httpContextAccessor, ILogger<BearerTokenHandler> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accessToken = _httpContextAccessor.HttpContext?.User?
                .FindFirst("access_token")?.Value;

            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "后端服务请求失败: {Method} {Url}",
                    request.Method, request.RequestUri);

                var body = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "后端服务暂时不可用"
                });

                return new HttpResponseMessage(HttpStatusCode.BadGateway)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    RequestMessage = request
                };
            }
        }
    }
}
