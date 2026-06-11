# API 集成设计 v1.2.0

## 概述

Admin 通过 HTTP API 调用 4 个后端服务。Phase 1 阶段不携带认证 token，所有请求直接发送。本文档记录每个服务的端点清单、调用方式及 Admin 侧的 HttpClient 配置。

## 通用约定

- 认证：Phase 1 无认证（后续接入 OpenIddict 后携带 `Authorization: Bearer {token}`）
- 内容类型：`application/json`
- 超时：API/Crawler/Sync 30 秒，Identity 10 秒
- 错误处理：非 2xx 响应 → 返回空数据 + 页面显示降级提示
- 命名 HttpClient：`ApiClient`、`CrawlerClient`、`SyncClient`、`IdentityClient`

## 1. Crawler API（:5078）

### 现有端点

| 方法 | 路径 | 说明 | 返回 |
|------|------|------|------|
| GET | `/api/crawlers` | 爬虫列表 | `[{name, displayName, isRunning, ...}]` |
| GET | `/api/crawlers/{name}/status` | 单个爬虫状态 | `{name, isRunning, totalBearingCount, pendingDetailCount, ...}` |
| POST | `/api/crawlers/{name}/run` | 手动触发运行 | 202 Accepted |

### Admin 侧使用

```csharp
// Program.cs
builder.Services.AddHttpClient("CrawlerClient", c => c.Timeout = TimeSpan.FromSeconds(30));

// CrawlerController
var baseUrl = _config["ApiUrls:FindBearingsCrawler"] ?? "https://localhost:7207";
var client = _factory.CreateClient("CrawlerClient");
var resp = await client.GetAsync($"{baseUrl}/api/crawlers");
```

## 2. Sync API（:5104）

### 现有端点

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/etl/run` | 触发全链 E→T→L |
| POST | `/api/etl/extract` | 触发 E 阶段 |
| POST | `/api/etl/transform` | 触发 T 阶段 |
| POST | `/api/etl/load` | 触发 L 阶段 |
| GET | `/api/etl/tasks/{taskId}` | 任务状态查询 |

### Admin 侧使用

```csharp
// Program.cs
builder.Services.AddHttpClient("SyncClient", c => c.Timeout = TimeSpan.FromSeconds(30));

// SyncController - 触发 ETL
var baseUrl = _config["ApiUrls:FindBearingsSync"] ?? "https://localhost:7206";
var client = _factory.CreateClient("SyncClient");
var resp = await client.PostAsync($"{baseUrl}/api/etl/run", null);
```

## 3. OpenFindBearings.Api（:7183）

### 现有端点

#### 品牌

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/brands` | 品牌列表（分页+搜索） |

#### 轴承类型

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/bearing-types` | 类型列表 |

#### 轴承

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/bearings/search` | 轴承搜索（分页） |

#### 商家

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/merchants` | 商家列表 |

#### 替代品

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/interchanges/by-bearing/{bearingId}` | 按轴承查替代品 |

#### 商家产品

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/merchants/{id}/bearings` | 商家在售商品 |

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

#### 健康检查

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/live` | 存活探针 |
| GET | `/ready` | 就绪探针 |

### Admin 代理端点

Admin 在 `Program.cs` 中注册了 2 个代理端点，供前端 AJAX 直接调用：

```csharp
// 替代品查询代理
app.MapGet("/api/proxy/interchanges/{bearingId:guid}", ...);

// 商家在售商品查询代理
app.MapGet("/api/proxy/merchant-bearings/{merchantId:guid}", ...);
```

### Admin 侧使用

```csharp
// Program.cs
builder.Services.AddHttpClient("ApiClient", c => c.Timeout = TimeSpan.FromSeconds(30));

// DataController
var apiBase = _config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183";
var client = _factory.CreateClient("ApiClient");
var resp = await client.GetAsync($"{apiBase}/api/bearings/search?page={page}&pageSize={pageSize}");
```

## 4. Identity API（:7201）

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

#### 健康检查

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/live` | 存活探针 |

### Admin 侧使用

```csharp
// Program.cs
builder.Services.AddHttpClient("IdentityClient", c => c.Timeout = TimeSpan.FromSeconds(10));

// UsersController
var identityBase = _config["ApiUrls:OpenFindBearingsIdentity"] ?? "https://localhost:7201";
var client = _factory.CreateClient("IdentityClient");
var resp = await client.GetAsync($"{identityBase}/api/account/admin/users?page={page}&pageSize={pageSize}");
```

## 5. 服务健康检测

Admin 通过 `ServiceHealthService` 并行检查 4 个服务的 `/live` 端点，结果通过 `/Home/Status` JSON 端点返回给前端 AJAX 轮询。

```csharp
// Services/ServiceHealthService.cs
public class ServiceHealthService
{
    public async Task<Dictionary<string, ServiceStatus>> CheckAllAsync()
    {
        // 并行检查 Api/Crawler/Sync/Identity 的 /live 端点
    }
}

// Controllers/HomeController.cs
[HttpGet]
public async Task<IActionResult> Status()
{
    var result = await _health.CheckAllAsync();
    return Json(result);
}
```

前端在 `site.js` 中通过 `fetch('/Home/Status')` 获取状态并更新顶栏圆点。

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始设计文档 |
| v1.1.0 | 2026-06-08 | 更新 Sync/Identity API 端点；补充 Crawler 无历史需 Admin 自行记录的策略 |
| v1.2.0 | 2026-06-11 | 对齐 Phase 1 实际实现：移除 Bearer token 要求；更新端点清单为实际调用的端点；新增代理端点文档；简化 ServiceHealthService 描述 |
