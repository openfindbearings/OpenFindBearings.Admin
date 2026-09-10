using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace OpenFindBearings.Admin.Services
{
    /// <summary>
    /// 将当前用户「有效的」访问令牌注入到所有后端 API 请求。
    /// 改动说明：原实现直接读 cookie 中的 access_token claim，令牌 10 分钟过期后不刷新，
    /// 导致登录后各管理页 10 分钟即 401（数据空白）。现改由 AdminTokenService 统一取用，
    /// 临期自动静默刷新并回写 cookie。
    /// </summary>
    public class BearerTokenHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AdminTokenService _tokenService;
        private readonly ILogger<BearerTokenHandler> _logger;

        // 改动说明：删除原 _tokenTask 实例字段缓存。IHttpClientFactory 会池化复用 handler 实例，
        // 实例字段会跨请求（甚至跨用户）返回首个已完成的 Task 结果（同一 token 字符串），
        // 导致令牌过期后该 handler 永远拿旧 token、401 不自愈。去重改由 AdminTokenService 内部
        // 按 userId 的静态缓存 + 单飞锁保证（并发只刷一次），本 handler 每次直接取用即可。

        public BearerTokenHandler(
            IHttpContextAccessor httpContextAccessor,
            AdminTokenService tokenService,
            ILogger<BearerTokenHandler> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _tokenService = tokenService;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            // 改动说明：/connect/*（如 code 换 token、revocation 走 Basic/表单 client 认证）与 /health*（匿名）
            // 绝不能用用户 Bearer 覆盖请求头，否则冲掉 Callback 设置的 Basic 认证 → ID2055 invalid_client。
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            bool skipBearer = path.StartsWith("/connect/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);

            if (!skipBearer && httpContext is not null)
            {
                // 每次直接取新鲜令牌（临期/过期由服务内部刷新；并发去重在服务侧静态缓存+锁完成）
                var token = await _tokenService.GetFreshAccessTokenAsync(httpContext);
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
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
