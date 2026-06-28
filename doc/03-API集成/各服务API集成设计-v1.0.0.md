# API 集成设计 v1.0.0

## 概述

Admin 通过 HTTP API 调用 4 个后端服务。所有调用走 HTTPS，携带从 OpenIddict 获取的 access_token。本文档记录每个服务的端点清单、调用方式及 Admin 侧的 HttpClient 封装。

## 通用约定

- 认证：所有请求 Header 携带 `Authorization: Bearer {access_token}`
- 内容类型：`application/json`
- 超时：默认 10 秒（长时间操作如触发爬虫可延长到 30 秒）
- 错误处理：非 2xx 响应 → 记录日志 + 页面显示错误消息
- Token 管理：Admin 维护 refresh_token，自动刷新

## 1. Crawler API（:5078）

### 现状

Crawler 当前已移除 Quartz，仅保留 CLI `--task=crawl` 模式 + 健康检查端点 + 管理端点。由于 Crawler 无运行历史持久化，Admin 触发爬虫后自行记录到本地 `CrawlerRunRecords` 表。

### 需要新增的端点

Admin 需要 Crawler 提供以下端点供 Admin 调用：

| 方法 | 路径 | 说明 | 返回 |
|------|------|------|------|
| GET | `/api/crawlers` | 爬虫列表（含状态概要） | `[{name, displayName, isRunning, ...}]` |
| GET | `/api/crawlers/{name}/status` | 单个爬虫详细状态 | `{name, isRunning, totalCount, completedCount, ...}` |
| POST | `/api/crawlers/{name}/run` | 手动触发单次运行 | 202 Accepted + `{name, startedAt}` |

由于爬虫运行时间长（~4h），触发端点返回 202 Accepted。Admin 轮询 status 查看完成状态，每次轮询到完成时写一条记录到 Admin 的 `CrawlerRunRecords` 表。

### Crawler 侧改动

Crawler 需要新增上述管理端点，并暴露运行状态供 Admin 查询。具体改动在 Crawler 项目中自行跟踪。

### Admin 侧封装

```csharp
public class CrawlerApiClient
{
    private readonly HttpClient _httpClient;

    // GET /api/crawlers
    Task<List<CrawlerSummary>> GetCrawlersAsync();

    // GET /api/crawlers/{name}/status
    Task<CrawlerStatus> GetCrawlerStatusAsync(string name);

    // POST /api/crawlers/{name}/run
    Task<RunJobResult> TriggerRunAsync(string name);

    // GET /api/crawlers/{name}/history
    Task<List<RunHistory>> GetRunHistoryAsync(string name);
}
```

## 2. Sync API（:5104）

### 现状

Sync API 已有完整的 ETL 触发端点。

### 端点清单

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/etl/run` | 触发全链 ETL |
| POST | `/api/etl/extract` | 触发 E 阶段 |
| POST | `/api/etl/transform` | 触发 T 阶段 |
| POST | `/api/etl/load` | 触发 L 阶段 |
| GET | `/api/etl/tasks/{taskId}` | 单任务状态查询 |
| GET | `/api/etl/status` | ETL 当前状态 |

### 注意

Sync API 自带 ETL 任务历史（通过 `EtlTaskDto`），Admin 无需额外记录。但 Admin 触发 ETL 的操作本身应记一笔 Admin 审计日志。

### Admin 侧封装

```csharp
public class SyncApiClient
{
    private readonly HttpClient _httpClient;

    Task<ApiResult> TriggerExtractAsync();
    Task<ApiResult> TriggerTransformAsync();
    Task<ApiResult> TriggerLoadAsync();
    Task<ApiResult> TriggerFullETLAsync();
    Task<ApiResult> TriggerRetryAsync();
    Task<ApiResult> TriggerStuckCheckAsync();
    Task<ETLStatus> GetStatusAsync();
    Task<List<ETLHistory>> GetHistoryAsync();
    Task<ETLStats> GetStatsAsync();
}
```

## 3. OpenFindBearings.Api（:7183）

### 现状

API 已有完整的 CRUD 端点，分页/搜索/批量操作均可用。

### 端点清单

#### 品牌

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/brands` | 品牌列表（分页+搜索） |
| GET | `/api/brands/{id}` | 品牌详情 |
| POST | `/api/brands` | 创建品牌 |
| PUT | `/api/brands/{id}` | 更新品牌 |
| DELETE | `/api/brands/{id}` | 删除品牌 |

#### 轴承类型

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/bearing-types` | 类型列表 |
| GET | `/api/bearing-types/{id}` | 类型详情 |
| POST | `/api/bearing-types` | 创建类型 |
| PUT | `/api/bearing-types/{id}` | 更新类型 |
| DELETE | `/api/bearing-types/{id}` | 删除类型 |

#### 轴承

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/bearings` | 轴承列表（分页+搜索+筛选） |
| GET | `/api/bearings/{id}` | 轴承详情 |
| PUT | `/api/bearings/{id}` | 更新轴承 |
| DELETE | `/api/bearings/{id}` | 删除轴承 |
| POST | `/api/bearings/batch-delete` | 批量删除 |

#### 商家

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/merchants` | 商家列表 |
| GET | `/api/merchants/{id}` | 商家详情 |
| PUT | `/api/merchants/{id}` | 更新商家 |
| DELETE | `/api/merchants/{id}` | 删除商家 |

#### 替代品

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/interchanges` | 替代品列表 |
| POST | `/api/interchanges` | 创建替代关系 |
| DELETE | `/api/interchanges/{id}` | 删除替代关系 |

#### 同步接口（M2M 白名单）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/sync/bearings/batch` | 批量创建/更新轴承 |
| POST | `/api/sync/brands/batch` | 批量创建/更新品牌 |
| POST | `/api/sync/bearing-types/batch` | 批量创建/更新类型 |
| POST | `/api/sync/merchants/batch` | 批量创建/更新商家 |
| POST | `/api/sync/merchant-bearings/batch` | 批量创建/更新商家-轴承关联 |
| POST | `/api/sync/interchanges/batch` | 批量创建/更新替代品 |

### Admin 侧封装

```csharp
public class BusinessApiClient
{
    private readonly HttpClient _httpClient;

    // 品牌
    Task<PagedResult<BrandDto>> GetBrandsAsync(BrandFilter filter);
    Task<BrandDto> GetBrandAsync(Guid id);
    Task<BrandDto> CreateBrandAsync(CreateBrandCommand cmd);
    Task<BrandDto> UpdateBrandAsync(Guid id, UpdateBrandCommand cmd);
    Task DeleteBrandAsync(Guid id);

    // 类型
    Task<List<BearingTypeDto>> GetBearingTypesAsync();
    Task<BearingTypeDto> GetBearingTypeAsync(Guid id);
    Task<BearingTypeDto> CreateBearingTypeAsync(CreateBearingTypeCommand cmd);
    Task<BearingTypeDto> UpdateBearingTypeAsync(Guid id, UpdateBearingTypeCommand cmd);
    Task DeleteBearingTypeAsync(Guid id);

    // 轴承
    Task<PagedResult<BearingDto>> GetBearingsAsync(BearingFilter filter);
    Task<BearingDto> GetBearingAsync(Guid id);
    Task<BearingDto> UpdateBearingAsync(Guid id, UpdateBearingCommand cmd);
    Task DeleteBearingAsync(Guid id);

    // 商家
    Task<PagedResult<MerchantDto>> GetMerchantsAsync(MerchantFilter filter);
    Task<MerchantDto> GetMerchantAsync(Guid id);
    Task<MerchantDto> UpdateMerchantAsync(Guid id, UpdateMerchantCommand cmd);
    Task DeleteMerchantAsync(Guid id);

    // 替代品
    Task<PagedResult<InterchangeDto>> GetInterchangesAsync(InterchangeFilter filter);
    Task<InterchangeDto> CreateInterchangeAsync(CreateInterchangeCommand cmd);
    Task DeleteInterchangeAsync(Guid id);
}
```

## 4. Identity API（:7201）

### 端点清单

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `~/connect/token` | OAuth2 令牌 |
| POST | `~/connect/authorize` | OAuth2 授权 |
| POST | `~/connect/logout` | 退出登录 |
| GET | `~/connect/userinfo` | 当前用户信息 |
| POST | `/api/account/signup` | 用户注册 |
| GET | `/api/account/me` | 获取个人资料 |
| GET | `/api/account/admin/users` | 用户列表（管理员） |
| POST | `/api/account/admin/users` | 创建用户（管理员） |
| PUT | `/api/account/admin/users/{id}` | 更新用户（管理员） |
| DELETE | `/api/account/admin/users/{id}` | 删除用户（管理员） |
| PATCH | `/api/account/admin/users/{id}/status` | 启用/禁用用户（管理员） |
| POST | `/api/role/` | 角色 CRUD（管理员） |
| GET | `/api/application/` | OAuth 客户端管理（管理员） |
| GET | `/api/auditlog/` | 审计日志查询（管理员） |

### Admin 侧封装

```csharp
public class IdentityApiClient
{
    private readonly HttpClient _httpClient;

    // 认证
    Task<TokenResponse> GetTokenAsync(string username, string password);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync();

    // 用户
    Task<List<UserDto>> GetUsersAsync();
    Task<UserDto> GetUserAsync(Guid id);
    Task<UserDto> CreateUserAsync(CreateUserCommand cmd);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserCommand cmd);
    Task DeleteUserAsync(Guid id);

    // 角色
    Task<List<RoleDto>> GetRolesAsync();
    Task<RoleDto> CreateRoleAsync(CreateRoleCommand cmd);
    Task<RoleDto> UpdateRoleAsync(Guid id, UpdateRoleCommand cmd);
    Task DeleteRoleAsync(Guid id);
}
```

## 5. 服务健康检测

Admin 启动一个后台 `BackgroundService`，每 15 秒轮询 4 个服务的 `/live` 端点：

```csharp
public class ServiceHealthService : BackgroundService
{
    private readonly ConcurrentDictionary<string, ServiceStatus> _statuses = new();

    public ServiceStatus GetStatus(string serviceName)
    {
        return _statuses.GetValueOrDefault(serviceName, ServiceStatus.Unknown);
    }

    public bool IsOnline(string serviceName) =>
        GetStatus(serviceName) == ServiceStatus.Online;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await CheckServiceAsync("Crawler", "http://localhost:5078/live");
            await CheckServiceAsync("Sync", "http://localhost:5104/live");
            await CheckServiceAsync("API", "http://localhost:7183/live");
            await CheckServiceAsync("Identity", "http://localhost:7201/live");
            await Task.Delay(15000, ct);
        }
    }
}
```

SignalR Hub 推送状态变化到前端，实现实时状态条更新。

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始设计文档 |
