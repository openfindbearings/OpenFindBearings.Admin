using System.Text.Json;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Services;

/// <summary>
/// 价格配置服务：缓存 API 端 /api/admin/config/price 的返回值（5 分钟 TTL）
/// 供在售商品弹窗、Excel 导入等场景使用
/// </summary>
public class PriceConfigService
{
    /// <summary>
    /// 缓存有效期（5 分钟）
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 读取失败后的退避时长（1 分钟）
    /// 改动说明：原实现失败时不更新缓存时间戳，导致 API 不可用时 Admin 每个页面请求都会
    ///           发起一次 HTTP 调用，与注释所写的"5 分钟后再试"不符
    /// </summary>
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<PriceConfigService> _logger;
    private static PriceConfigDto? _cached;
    private static DateTime _cachedAt = DateTime.MinValue;

    /// <summary>
    /// 读取失败后的重试时刻
    /// 改动说明：退避原先依赖 _cached 非空才生效，冷启动首次失败或 Invalidate 之后 API 不可用时
    ///           _cached 恒为 null，退避被完全绕过，导致每个页面请求都发起一次 HTTP 调用
    /// </summary>
    private static DateTime _nextRetryAt = DateTime.MinValue;

    private static readonly SemaphoreSlim _gate = new(1, 1);

    public PriceConfigService(
        IHttpClientFactory factory,
        IConfiguration config,
        ILogger<PriceConfigService> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前价格配置（进程内缓存 5 分钟）
    /// </summary>
    public async Task<PriceConfigDto> GetAsync(CancellationToken ct = default)
    {
        // 改动说明：缓存命中检查原在 _gate.WaitAsync 之后，使所有请求（含命中）都被串行化
        //           通过同一把锁。该服务注册为 Singleton，命中路径本应无锁，否则成为全站瓶颈
        if (_cached != null && DateTime.UtcNow - _cachedAt < CacheDuration)
            return _cached;

        // 处于失败退避窗口内且无缓存可用时，直接返回默认值，不再打接口
        if (_cached == null && DateTime.UtcNow < _nextRetryAt)
            return new PriceConfigDto();

        await _gate.WaitAsync(ct);
        try
        {
            // 双重检查，避免并发下重复请求
            if (_cached != null && DateTime.UtcNow - _cachedAt < CacheDuration)
                return _cached;

            var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
            var client = _factory.CreateClient("ApiClient");
            try
            {
                var resp = await client.GetAsync($"{apiBase}/api/admin/config/price", ct);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var data))
                    {
                        var dto = JsonSerializer.Deserialize<PriceConfigDto>(data.GetRawText(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (dto != null)
                        {
                            _cached = dto;
                            _cachedAt = DateTime.UtcNow;
                            return _cached;
                        }
                    }
                }
                _logger.LogWarning("获取价格配置失败: {Status}", resp.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取价格配置异常");
            }

            // 失败时设置重试时刻，使退避窗口与 _cached 是否为 null 无关
            // 改动说明：原先只在 _cachedAt 上做偏移，而快速路径要求 _cached 非空才生效，
            //           冷启动首次失败或 Invalidate 后 API 不可用时退避被完全绕过
            _nextRetryAt = DateTime.UtcNow.Add(FailureBackoff);
            return _cached ?? new PriceConfigDto();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 主动失效缓存（Admin 更新配置后调用）
    /// </summary>
    /// 改动说明：同时清空失败重试时刻，否则在失败退避窗口内调用本方法后，
    ///           下一次读取会被退避判断短路，管理员改完配置要等退避结束才看得到新值
    public static void Invalidate()
    {
        _cached = null;
        _cachedAt = DateTime.MinValue;
        _nextRetryAt = DateTime.MinValue;
    }
}
