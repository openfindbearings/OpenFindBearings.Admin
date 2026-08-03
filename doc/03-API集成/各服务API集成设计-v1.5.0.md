# API 集成设计 v1.5.0

## 概述

Admin 通过 HTTP API 调用 3 个后端服务（API、Sync、Identity）。Phase 1 已通过 BearerTokenHandler 自动从登录 cookie 中提取 JWT 注入到 API 请求。本文档记录每个服务的端点清单、调用方式及 Admin 侧的 HttpClient 配置。

Crawler 项目以纯 CronJob 模式运行（不常驻 Web 实例），Admin 不直接调用 Crawler API；已接入的爬虫数据源清单经 Sync `GET /api/datasources` 只读获取。

## 通用约定

- 认证：通过 BearerTokenHandler 自动从 cookie `access_token` claim 注入 `Authorization: Bearer {token}`（仅 ApiClient / SyncClient）
- 内容类型：`application/json`（文件上传使用 `multipart/form-data`）
- 超时：API/Sync 30 秒，Identity 10 秒
- 错误处理：非 2xx 响应 → 返回空数据 + 页面显示降级提示
- 命名 HttpClient：`ApiClient`（带 BearerTokenHandler）、`SyncClient`（带 BearerTokenHandler）、`IdentityClient`

## 1. Sync API（:7206）

### 现有端点

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/etl/extract` | 触发 E 阶段 |
| POST | `/api/etl/transform` | 触发 T 阶段 |
| POST | `/api/etl/load` | 触发 L 阶段 |
| POST | `/api/etl/run` | 触发全链 E→T→L |
| GET | `/api/etl/tasks/{taskId}` | 任务状态查询 |
| GET | `/api/audit/stats` | 待审核统计（按实体类型计数） |
| GET | `/api/audit/pending` | 待审核列表 |
| GET | `/api/config/brands` | 品牌映射 CRUD |
| GET | `/api/config/types` | 类型映射 CRUD |
| GET | `/api/audit-log` | 审计日志（分页） |
| GET | `/api/inventory/template` | 下载库存导入 Excel 模板 |
| POST | `/api/inventory/import` | 上传 Excel 导入库存 |
| GET | `/api/datasources` | 已接入爬虫数据源清单 |

### Admin 侧使用

```csharp
// Program.cs
builder.Services.AddHttpClient("SyncClient", c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddHttpMessageHandler<BearerTokenHandler>();

// SyncController - 触发 ETL
var baseUrl = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
var client = _factory.CreateClient("SyncClient");
var resp = await client.PostAsync($"{baseUrl}/api/etl/extract", null);
```

## 2. OpenFindBearings.Api（:7183）

### 现有端点

#### 品牌

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/brands` | 品牌列表（分页+搜索） |
| POST | `/api/admin/brands` | 创建品牌 |
| PUT | `/api/admin/brands/{id}` | 编辑品牌 |
| DELETE | `/api/admin/brands/{id}` | 软删除品牌 |
| PUT | `/api/admin/brands/{id}/restore` | 恢复已删除品牌 |
| DELETE | `/api/admin/brands/{id}/hard` | 彻底删除品牌（需 DataRestore 权限） |

#### 轴承类型

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/bearing-types` | 类型列表 |
| POST | `/api/admin/bearing-types` | 创建类型 |
| PUT | `/api/admin/bearing-types/{id}` | 编辑类型 |
| DELETE | `/api/admin/bearing-types/{id}` | 软删除类型 |
| PUT | `/api/admin/bearing-types/{id}/restore` | 恢复已删除类型 |
| DELETE | `/api/admin/bearing-types/{id}/hard` | 彻底删除类型 |

#### 轴承

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/bearings/search` | 轴承搜索（分页） |
| POST | `/api/admin/bearings` | 创建轴承 |
| PUT | `/api/admin/bearings/{id}` | 编辑轴承 |
| DELETE | `/api/admin/bearings/{id}` | 软删除轴承 |
| PUT | `/api/admin/bearings/{id}/restore` | 恢复已删除轴承 |

#### 商家

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/merchants` | 商家列表 |
| POST | `/api/admin/merchants` | 创建商家 |
| PUT | `/api/admin/merchants/{id}` | 编辑商家 |
| DELETE | `/api/admin/merchants/{id}` | 软删除商家 |
| PUT | `/api/admin/merchants/{id}/restore` | 恢复已删除商家 |

#### 替代品

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/interchanges/by-bearing/{bearingId}` | 按轴承查替代品 |

#### 商家产品

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/merchants/{id}/bearings` | 商家在售商品（支持 dataSource 过滤） |

#### 纠错审核

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/corrections` | 纠错列表（分页+状态筛选） |
| POST | `/api/corrections/{id}/approve` | 审批通过 |
| POST | `/api/corrections/{id}/reject` | 审批拒绝 |

#### 营业执照

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/merchants/licenses` | 营业执照列表 |
| POST | `/api/merchants/licenses/{id}/approve` | 通过 |
| POST | `/api/merchants/licenses/{id}/reject` | 拒绝 |

#### 商家认证

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/merchants/{id}/verify` | 认证商家 |

#### 系统配置

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/config` | 配置列表 |
| PUT | `/api/config/{key}` | 更新配置 |

#### 仪表盘统计

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/admin/dashboard/stats` | 仪表盘统计（轴承/品牌/商家总数 + 待审核数） |

### Admin 代理端点

Admin 在 `Program.cs` 中注册了多个代理端点，供前端 AJAX 直接调用：

```csharp
// 替代品查询代理
app.MapGet("/api/proxy/interchanges/{bearingId:guid}", ...);

// 商家在售商品查询代理
app.MapGet("/api/proxy/merchant-bearings/{merchantId:guid}", ...);

// Excel 批量导入在售轴承（转发到 Sync API）
app.MapPost("/api/proxy/excel/import-bearing", ...);

// 下载 Excel 导入模板
app.MapGet("/api/proxy/excel/template", ...);
```

### Admin 侧使用

```csharp
// Services/BearerTokenHandler.cs
public class BearerTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    // 自动从 HttpContext.User.Claims 提取 access_token 注入请求头
}

// Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient("ApiClient", c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddHttpMessageHandler<BearerTokenHandler>();

// HomeController - 仪表盘统计
var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
var client = _factory.CreateClient("ApiClient");
var resp = await client.GetAsync($"{apiBase}/api/admin/dashboard/stats");
```

## 3. Identity API（:7201）

### 现有端点

#### 用户管理

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/account/admin/users` | 用户列表（分页+搜索） |
| POST | `/api/account/admin/users/{id}/toggle-status` | 启用/禁用 |
| POST | `/api/account/admin/users/{id}/restore` | 恢复已删除用户 |

#### 审计日志

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/auditlog` | 审计日志列表（分页） |

### Admin 侧使用

```csharp
// Program.cs
builder.Services.AddHttpClient("IdentityClient", c => c.Timeout = TimeSpan.FromSeconds(10));

// UsersController
var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
var client = _factory.CreateClient("IdentityClient");
var resp = await client.GetAsync($"{identityBase}/api/account/admin/users?page={page}&pageSize={pageSize}");
```

## 4. 数据爬虫展示（Sync 数据源）

爬虫数据源清单不写死，Admin 从 Sync `GET /api/datasources` 动态读取（过滤 `DataSourceType.Database`，即网站爬虫）。新增爬虫网站只需 Crawler 插件 + Sync 注册 IDataSource，Admin 零改动自动显示。

```csharp
// HomeController - 数据源清单
[Authorize]
public async Task<IActionResult> DataSources()
{
    var syncBase = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
    var client = _factory.CreateClient("SyncClient");
    var resp = await client.GetAsync($"{syncBase}/api/datasources");
    // 返回 [{ name, displayName, supportsIncremental }]
}
```

## 5. 服务健康检测

Admin 通过 `ServiceHealthService` 检查 3 个服务（API、Sync、Identity）的 `/health/live` 端点，结果通过 `/Home/Status` JSON 端点返回给前端 AJAX 轮询。

```csharp
// Services/ServiceHealthService.cs
public class ServiceHealthService
{
    public async Task<Dictionary<string, ServiceStatus>> CheckAllAsync()
    {
        // 并行检查 Api/Sync/Identity 的 /health/live 端点
    }
}

// Controllers/HomeController.cs
[AllowAnonymous]
public async Task<IActionResult> Status()
{
    var result = await _health.CheckAllAsync();
    return Json(result);
}
```

前端通过 `fetch('/Home/Status')` 获取状态并更新服务状态表格。

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始设计文档 |
| v1.1.0 | 2026-06-08 | 更新 Sync/Identity API 端点；补充 Crawler 无历史需 Admin 自行记录的策略 |
| v1.2.0 | 2026-06-11 | 对齐 Phase 1 实际实现：移除 Bearer token 要求；更新端点清单为实际调用的端点；新增代理端点文档；简化 ServiceHealthService 描述 |
| v1.3.0 | 2026-06-11 | 新增品牌/类型/轴承/商家 CRUD 端点（创建/编辑/软删除/恢复/彻底删除）；新增 Excel 导入代理端点（POST + 模板下载）；端点清单从 15 个扩展到 35+ 个 |
| v1.4.0 | 2026-06-15 | 新增 BearerTokenHandler 自动注入 JWT；新增仪表盘统计端点 `/api/admin/dashboard/stats`；ApiClient 注册添加 AddHttpMessageHandler |
| v1.5.0 | 2026-08-03 | 移除 Crawler API 章节（Crawler 降级为纯 CronJob）；新增 Sync `/api/datasources` 端点及数据爬虫展示章节；健康检测收敛为 3 服务并统一 /health/live 路径；Sync 端口更正为 7206；HttpClient 移除 CrawlerClient |
