using System.Text.Json;
using OpenFindBearings.Admin.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models.DTOs;

namespace OpenFindBearings.Admin.Controllers;

/// <summary>
/// 替代品审核控制器（v2.16.0 替代品治理）：商家提交替代候选的人工审核出口。
/// 待审队列服务端渲染（对齐商品审核页），通过/驳回由浏览器调用
/// Program.cs 注册的代理端点 /api/proxy/admin-interchanges/{id}/approve|reject 完成。
/// 权限 merchant.verify（与商品审核、入驻审批同口径）。
/// </summary>
[Authorize]
[PanelPermission("merchant.verify")]
public class InterchangeReviewController : Controller
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<InterchangeReviewController> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public InterchangeReviewController(IHttpClientFactory factory, IConfiguration config, ILogger<InterchangeReviewController> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    /// <summary>API 基址配置（与各控制器同口径）</summary>
    private string ApiBase() => _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";

    /// <summary>
    /// 待审核替代候选队列页：拉取 API /api/admin/interchanges/pending，服务端渲染首屏与分页。
    /// API 不可达时降级空表并提示，不阻断页面。
    /// </summary>
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
    {
        var client = _factory.CreateClient("ApiClient");
        var url = $"{ApiBase()}/api/admin/interchanges/pending?page={page}&pageSize={pageSize}";
        try
        {
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("待审核替代候选 API 请求失败: {Status}", resp.StatusCode);
                ViewBag.Error = "无法获取待审核替代候选列表";
            }
            else
            {
                var json = await resp.Content.ReadAsStringAsync();
                var apiResp = JsonSerializer.Deserialize<ApiResponse<PagedData<InterchangeAdminRowDto>>>(json, JsonOpts);
                ViewBag.Items = apiResp?.Data?.Items ?? [];
                ViewBag.TotalCount = apiResp?.Data?.TotalCount ?? 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取待审核替代候选列表异常");
            ViewBag.Error = "连接 API 服务异常";
        }

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View();
    }
}