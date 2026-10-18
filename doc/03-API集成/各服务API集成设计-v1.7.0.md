# 各服务API集成设计 v1.7.0

> 版本：v1.7.0 | 日期：2026-10-18 | 上一版：v1.6.0

## 一、概述

Admin 通过 HTTP API 调用 **2 个后端服务**（API、Identity），并作为 OIDC 客户端接入 Identity 的授权码流程。Admin 自身**无本地数据库**：权限目录与角色均由 API 统一托管（单一来源），Admin 只做展示与转发。

未启用本仓库全部能力的部署方，可经扩展模块（`IAdminModule`，见第七节）注入额外的 HttpClient、代理端点、健康探测、侧边栏条目与 `Views/Ext/*.cshtml` 槽位；开源版本内置零个模块实现，故所有扩展槽位不渲染。

## 二、通用约定

- **认证**：`BearerTokenHandler` 自动从登录 cookie 的 `access_token` 注入 `Authorization: Bearer {token}`；跳过 `/connect/*`、`/health*` 路径；连接层异常（`HttpRequestException`）时合成 502 JSON 返回（便于前端降级提示）。
- **命名 HttpClient**：`ApiClient`（30s）、`IdentityClient`（10s），二者均挂 `BearerTokenHandler`；开发环境（DEBUG）跳过自签证书校验。
- **地址解析**：`Helpers/ApiUrlResolver.cs` 统一读取配置键——`ApiUrls:OpenFindBearingsApi`（缺省 `https://localhost:7183`）、`ApiUrls:OpenFindBearingsIdentity`（缺省 `https://localhost:7201`）。
- **配置优先级**：`appsettings.json` < `appsettings.pro.json`（可选，缺省不存在）< **环境变量**。`Program.cs` 在 `AddJsonFile("appsettings.pro.json")` 之后**再调用一次 `AddEnvironmentVariables()`**，确保 K8s 环境变量能覆盖镜像内烘入的扩展配置。
- **内容类型**：`application/json`；文件上传使用 `multipart/form-data`。
- **错误处理**：非 2xx 响应 → 返回空数据 + 页面降级提示（不抛异常打断页面）。
- **连接串**：Admin 不再持有任何数据库连接串。

## 三、OpenFindBearings.Api（经 `ApiClient`）

### 自身与登录态

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/me/permissions` | 当前管理员权限（cookie 校验与登录后加载） |
| GET | `/api/me/roles` | 当前用户角色 |

### 仪表盘

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/admin/dashboard/stats` | 仪表盘统计（含按来源拆分的轴承/商家/替代品新增与下钻口径） |

### 主数据：品牌 / 类型 / 轴承

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/admin/brands` | 品牌列表（含已删除） |
| POST | `/api/admin/brands` | 创建品牌 |
| PUT / DELETE | `/api/admin/brands/{id}` | 编辑 / 软删除 |
| PUT | `/api/admin/brands/{id}/restore` | 恢复 |
| DELETE | `/api/admin/brands/{id}/hard` | 彻底删除（`data.harddelete`） |
| GET / POST | `/api/admin/bearing-types` | 类型列表 / 创建 |
| PUT / DELETE | `/api/admin/bearing-types/{id}` | 编辑 / 软删除 |
| PUT | `/api/admin/bearing-types/{id}/restore` | 恢复 |
| DELETE | `/api/admin/bearing-types/{id}/hard` | 彻底删除 |
| GET / POST | `/api/admin/bearings` | 轴承列表（关键字 + 创建时间 + 来源筛选）/ 创建 |
| PUT / DELETE | `/api/admin/bearings/{id}` | 编辑 / 软删除 |
| PUT | `/api/admin/bearings/{id}/restore` | 恢复 |
| DELETE | `/api/admin/bearings/{id}/hard` | 彻底删除 |

### 商家

| 方法 | 路径 | 用途 |
|---|---|---|
| GET / POST | `/api/admin/merchants` | 商家列表（含入驻时间/来源筛选）/ 创建 |
| PUT / DELETE | `/api/admin/merchants/{id}` | 编辑 / 软删除 |
| PUT | `/api/admin/merchants/{id}/restore` | 恢复 |
| DELETE | `/api/admin/merchants/{id}/hard` | 彻底删除 |
| POST | `/api/admin/merchants/{id}/detach` | 与当前绑定用户解绑 |
| GET | `/api/admin/merchants/{id}` | 商家详情 |
| GET | `/api/admin/merchants/{id}/documents` | 商家证照材料 |
| POST | `/api/admin/merchants/{id}/approve` | 入驻申请审批通过（写入 `OnboardedAt`） |
| POST | `/api/admin/merchants/{id}/verify` | 认证商家 |
| POST | `/api/admin/merchants/{id}/reject` | 驳回入驻/认证 |
| GET | `/api/merchants/{id}/bearings` | 该商家在售商品（支持 `onlyOnSale`/`dataSource` 过滤） |

### 替代品（平台维护的主数据，无审核队列）

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/admin/interchanges` | 列表（关键字 + 创建时间 + 来源筛选） |
| POST | `/api/admin/interchanges` | 新建（源/目标型号 + 类型 + 置信度 + 双向，进 API 即生效） |
| PUT / DELETE | `/api/admin/interchanges/{id}` | 编辑 / 物理删除 |

### 商品审核（公域上架审核）

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/admin/merchant-bearings/pending` | 待审在售商品 |
| POST | `/api/admin/merchant-bearings/{id}/approve` | 审核通过（转出售中，公域可见） |
| POST | `/api/admin/merchant-bearings/{id}/reject` | 驳回（附理由） |

### 审核类（纠错 / 证照）

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/admin/corrections` | 纠错列表（分页 + 状态筛选） |
| POST | `/api/admin/corrections/{id}/approve` | 审批通过 |
| POST | `/api/admin/corrections/{id}/reject` | 审批拒绝 |
| GET | `/api/admin/documents/pending` | 证照待审列表 |
| POST | `/api/admin/documents/{id}/approve` | 通过 |
| POST | `/api/admin/documents/{id}/reject` | 拒绝 |

### 寻货 / 勋章 / 商城

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/admin/sourcing/demands` | 寻货单列表 |
| GET | `/api/admin/sourcing/demands/{id}` | 寻货单详情 |
| POST | `/api/admin/sourcing/demands/{id}/takedown` | 下架寻货单 |
| GET | `/api/admin/achievements` | 勋章列表 |
| PUT | `/api/admin/achievements/{id}` | 编辑勋章 |
| POST | `/api/admin/achievements/{id}/image` | 上传勋章图（multipart） |
| GET | `/api/admin/mall/items` | 积分商城商品列表 |
| PUT | `/api/admin/mall/items/{id}` | 编辑商城商品 |
| GET | `/api/admin/mall/gifts/pending` | 商家挂礼待审 |
| POST | `/api/admin/mall/gifts/{id}/approve` | 挂礼通过 |
| POST | `/api/admin/mall/gifts/{id}/reject` | 挂礼驳回 |
| GET | `/api/admin/mall/orders/escrow` | 托管中兑换单 |
| POST | `/api/admin/mall/orders/{id}/refund` | 兑换单退款 |

### 积分与系统配置

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/admin/points/rules` | 积分规则列表 |
| PUT | `/api/admin/points/rules/{id}` | 更新规则 |
| GET | `/api/admin/points/levels` | 等级配置 |
| PUT | `/api/admin/points/levels/{id}` | 更新等级 |
| GET | `/api/admin/points/merchant-tasks` | 商家任务列表 |
| PUT | `/api/admin/points/merchant-tasks/{id}` | 更新任务 |
| POST | `/api/admin/points/merchant-tasks` | 新建任务 |
| GET | `/api/admin/config` | 系统配置列表 |
| PUT | `/api/admin/config/{key}` | 更新配置 |
| GET | `/api/admin/config/price` | 价格配置（只读；`PriceConfigService` 缓存 5 分钟，失败 1 分钟退避） |

### 权限与角色

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/admin/permissions` | 权限目录（单一来源，Admin 不本地维护） |
| GET | `/api/admin/roles` | 角色列表 |
| GET | `/api/admin/roles/all` | 全部角色（下拉用） |
| GET | `/api/admin/roles/{id}/permissions` | 角色权限 |
| POST | `/api/admin/roles` | 新建角色 |
| DELETE | `/api/admin/roles/{id}` | 删除角色 |
| POST | `/api/admin/roles/{id}/permissions` | 保存角色权限 |
| GET | `/api/admin/users/platform-roles` | 平台角色清单 |
| POST | `/api/admin/users/provision` | 为用户开通平台角色 |
| GET | `/api/admin/users/by-auth/{id}/roles` | 用户已分配角色 |
| DELETE | `/api/admin/users/by-auth/{id}/roles/{role}` | 撤销用户角色 |

### 公域查询（供代理转发）

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/bearings/search` | 轴承搜索（替代品新建时选型号） |
| GET | `/api/bearings/{id}/merchants` | 轴承在售商家（反向查询） |
| GET | `/api/bearings/{id}/interchanges` | 轴承替代关系（详情弹窗） |

## 四、Identity（经 `IdentityClient` 与 OIDC）

### OIDC 流程

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/connect/authorize` | 授权码跳转 |
| POST | `/connect/token` | 授权码换令牌 / 刷新令牌（`AdminTokenService`，按 `device_id` 进程内缓存 + 单飞锁） |
| GET | `/connect/logout` | 退出登录 |
| GET | `/api/account/me` | 当前账户信息 |

### 用户管理

| 方法 | 路径 | 用途 |
|---|---|---|
| GET / POST | `/api/account/admin/users` | 用户列表（分页）/ 新建用户 |
| PATCH | `/api/account/admin/users/{id}/status` | 启用/禁用 |
| POST | `/api/account/admin/users/{id}/unlock` | 解锁 |
| POST | `/api/account/admin/users/{id}/reset-to-default` | 重置密码 |
| POST | `/api/account/admin/users/{id}/restore` | 恢复已删除用户 |
| DELETE | `/api/account/admin/users/{id}` | 软删除 |
| DELETE | `/api/account/admin/users/{id}/permanent` | 物理删除（仅已软删） |

### 审计日志与健康

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/api/auditlog` | 审计日志（Identity 源，分页） |
| GET | `/health/live` | 健康探测 |

## 五、Admin 代理端点（`/api/proxy/*`，共 11 条）

供视图内 AJAX 直接调用（均 `RequireAuthorization`；写操作由服务端转发并透传失败原因）：

| 方法 | Admin 路径 | 转发到 |
|---|---|---|
| GET | `/api/proxy/interchanges/{bearingId}` | `GET {api}/api/bearings/{bearingId}/interchanges` |
| GET | `/api/proxy/merchant-bearings/{merchantId}` | `GET {api}/api/merchants/{merchantId}/bearings` |
| GET | `/api/proxy/bearing-merchants/{bearingId}` | `GET {api}/api/bearings/{bearingId}/merchants` |
| GET | `/api/proxy/merchant-bearings/pending` | `GET {api}/api/admin/merchant-bearings/pending` |
| POST | `/api/proxy/merchant-bearings/{id}/approve` | `POST {api}/api/admin/merchant-bearings/{id}/approve` |
| POST | `/api/proxy/merchant-bearings/{id}/reject` | `POST {api}/api/admin/merchant-bearings/{id}/reject` |
| POST | `/api/proxy/admin-interchanges` | `POST {api}/api/admin/interchanges` |
| PUT | `/api/proxy/admin-interchanges/{id}` | `PUT {api}/api/admin/interchanges/{id}` |
| DELETE | `/api/proxy/admin-interchanges/{id}` | `DELETE {api}/api/admin/interchanges/{id}` |
| GET | `/api/proxy/bearing-search` | `GET {api}/api/bearings/search` |
| POST | `/api/proxy/achievements/{id}/image` | `POST {api}/api/admin/achievements/{id}/image`（multipart） |

> 替代品管理页的**列表**由 `InterchangesController` 服务端直取 API（不经代理）；代理只承载新建/编辑/删除三个写操作与型号搜索。

## 六、服务健康检测

`Services/ServiceHealthService.cs` 并行探测两个内置服务：`Api`（`{ApiUrl}/health/live`，经 `ApiClient`）、`Identity`（`{IdentityUrl}/health/live`，经 `IdentityClient`）；扩展模块可经 `module.AddHealthServices` 追加。结果由 `HomeController.Status` 以 `/Home/Status` JSON 返回，前端（`ServiceStatusViewComponent` 渲染的骨架 + 遍历脚本）逐行更新状态。

## 七、扩展点（未启用本仓库全部能力的部署方）

| 扩展点 | 作用 |
|---|---|
| `IAdminModule.IsEnabled(config)` | 决定模块是否装载（`AdminModuleLoader.Discover` 反射发现并过滤） |
| `IAdminModule.MapRoutes(app)` | 追加代理端点 |
| `IAdminModule.ConfigureServices(...)` | 追加命名 HttpClient 等服务 |
| `IAdminModule.AddHealthServices(...)` | 追加健康探测目标（自动出现在 `/Home/Status` 与登录页服务状态） |
| 侧边栏条目注入 | 审核组条目、顶栏条目、自定义分组（按 `item.Permission` 显示） |
| 仪表盘 JSON 增强 | 额外统计字段 |
| 审计日志来源 | 追加 source 标签页 |
| `Views/Ext/{key}.cshtml` | `ProSlot` 槽位分部视图（无分部则不渲染） |

## 八、变更历史

| 版本 | 日期 | 变更 |
|---|---|---|
| v1.7.0 | 2026-10-18 | **按代码全量校正**：① 服务收敛为 API + Identity（Admin 不再调用任何互联网数据管线服务，删除原"Sync API"章节、ETL/Excel/数据源代理与 `SyncClient`）；② 端点清单按实测重写（补替代品字典化、商品审核、寻货、勋章、商城、积分、权限/角色、平台角色等域，共 10+ 域）；③ 代理端点 7 → **11 条**（更新为当前真实清单）；④ 健康检测由 3 服务改 2 服务并说明扩展模块追加；⑤ 新增"扩展点"章节；⑥ 补配置优先级（pro.json 之后 `AddEnvironmentVariables`）与 `ApiUrlResolver` 说明；⑦ 文档标题与文件名统一为"各服务API集成设计" |
| v1.6.0 | 2026-09-11 | 代码审查对齐：纠错/营业执照/商家认证端点路径修正，代理端点补充至 7 条，Identity 用户管理补 unlock/permanent/restore |
| v1.5.0 | 2026-08-03 | 移除 Crawler API 章节（Crawler 降级为纯 CronJob）；新增数据源端点与数据爬虫展示章节 |
| v1.4.0 | 2026-06-15 | 新增 BearerTokenHandler 自动注入 JWT；新增仪表盘统计端点 |
| v1.3.0 | 2026-06-11 | 新增品牌/类型/轴承/商家 CRUD 与 Excel 导入代理端点 |
| v1.2.0 | 2026-06-11 | 对齐 Phase 1 实际实现；新增代理端点文档；简化健康服务描述 |
| v1.1.0 | 2026-06-08 | 更新后端 API 端点 |
| v1.0.0 | 2026-06-08 | 初始设计文档 |
