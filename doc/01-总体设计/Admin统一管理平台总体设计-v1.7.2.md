# Admin 统一管理平台总体设计 v1.7.2

版本：v1.7.2
日期：2026-10-18
上一版：v1.7.1

## 概述

OpenFindBearings.Admin 是一个统一管理后台，将 OpenFindBearings.Api 业务数据与 OpenFindBearings.Identity 用户认证的运维管理整合到单一 ASP.NET Core MVC 项目中。认证采用 OAuth 授权码流程登录 Identity，随后由 BearerTokenHandler 把当前用户的访问令牌注入所有后端调用。

Admin 自身不持有本地数据库：业务数据一律经 API 代理获取，面板角色与权限目录由 API 托管，Admin 仅在登录时从 API 拉取并写入 Cookie 声明用于界面与控制器判权。

开源版仅对接 OpenFindBearings.Api 与 OpenFindBearings.Identity 两个后端服务。基座通过通用扩展点（`Ext/`）为未启用本能力的部署或扩展模块预留接入位置，基座本身不含这些能力。

## 架构总览

```
浏览器
  | HTTP/HTTPS
  v
OpenFindBearings.Admin（ASP.NET Core MVC）
  | OAuth 授权码流程登录（Identity /connect/authorize）
  | BearerTokenHandler 从 Cookie 取令牌并注入 ApiClient / IdentityClient
  |
  +--> OpenFindBearings.Api（:7183）      品牌/类型/轴承/商家/替代品/寻货/纠错/配置/仪表盘统计/权限目录
  +--> OpenFindBearings.Identity（:7201） 用户账号管理、审计日志、认证
```

## 架构决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 认证 | OAuth 授权码流程 → BearerTokenHandler 注入令牌 | 后端 API 请求直接复用前端登录认证，无需重复登录 |
| 数据访问 | 不设本地库，一律经命名 HttpClient 代理 API | Admin 只做界面与代理，业务与权限的唯一事实源留在 API |
| 权限判定 | Cookie 中的 panel_role / permission 多值 claim | 登录时拉取 API RBAC 写入，零跨服务调用，30 分钟复核防残留 |
| 部署 | K3s Deployment（1 副本） | Dockerfile + deploy/ 清单模板 |
| CSS/JS | libman 管理，集中存放于 wwwroot/lib/ | 不分散在各页面 |

## 数据访问原则

| 数据 | 方式 | 说明 |
|------|------|------|
| 品牌/类型/轴承/商家 | 调 API | 业务数据，API 有权限与审计 |
| 替代品、寻货 | 调 API | 平台替代关系治理、寻货治理均代理 API |
| 纠错、入驻、证照材料、商品审核 | 调 API | 审核队列与处置均在 API |
| 用户账号 | 调 Identity API | 账号启用/禁用/解锁/重置密码/恢复/物理删除 |
| 面板角色、权限目录 | 调 API | 真相源在 API RBAC 表，Admin 只代理只读或写入 |
| 审计日志 | 调 Identity API 与 API | 管理员日志源与 API 日志源 |
| 仪表盘统计 | 调 API | `/api/admin/dashboard/stats`，扩展模块可富化 |

## 项目结构

```
OpenFindBearings.Admin/
+-- src/OpenFindBearings.Admin/
|   +-- Program.cs                     — 应用入口、认证接线、健康检查、API 代理端点、扩展模块接入
|   +-- appsettings.json               — 服务地址、Identity 客户端配置（无连接串）
|   +-- appsettings.Development.json   — 开发环境覆盖（Identity ClientSecret）
|   +-- libman.json                    — 前端库清单（Bootstrap 5 + jQuery + Font Awesome 6）
|   +-- Dockerfile                     — 多阶段构建，aspnet:10.0 / sdk:10.0，EXPOSE 8080/8081
|   |
|   +-- Controllers/                   — 17 个控制器
|   |   +-- AccountController.cs       — Login/LoginAction/Callback/Logout/SignoutCallbackOidc/Profile
|   |   +-- HomeController.cs          — 仪表盘 + /Home/Status + /Home/DashboardStats
|   |   +-- DataController.cs          — 品牌/类型/轴承/商家列表与治理
|   |   +-- CorrectionController.cs    — 信息纠错审核
|   |   +-- MerchantVerifyController.cs — 入驻申请审批
|   |   +-- DocumentController.cs      — 证照材料审核
|   |   +-- MerchantBearingReviewController.cs — 商品审核
|   |   +-- InterchangesController.cs  — 替代品管理
|   |   +-- SourcingController.cs      — 寻货管理
|   |   +-- UsersController.cs         — 用户管理（调 Identity，含解锁/封禁/物理删除/角色分配）
|   |   +-- RoleController.cs          — 角色管理（代理 API RBAC）
|   |   +-- PermissionController.cs    — 权限管理（代理 API 权限目录，只读）
|   |   +-- AuditLogController.cs      — 审计日志（多源）
|   |   +-- ConfigController.cs        — 系统配置（代理 API，含积分任务页签）
|   |   +-- PointsController.cs        — 积分任务批量保存端点
|   |   +-- MallController.cs          — 商城目录管理
|   |   +-- AchievementController.cs   — 成就目录管理
|   |
|   +-- Authorization/
|   |   +-- PanelRoles.cs              — 面板角色全集 Admin/Operator/Auditor
|   |   +-- PanelPermission.cs         — PanelPermission 特性 + 动态策略提供器 + 处理器
|   |
|   +-- Ext/                           — 通用扩展点
|   |   +-- AdminExt.cs                — IAdminModule 接口、AdminMenuItem、ServiceEndpoint
|   |   +-- AdminModuleLoader.cs       — 反射发现启用的扩展模块
|   |
|   +-- Helpers/
|   |   +-- ApiUrlResolver.cs          — 后端基地址解析（ApiUrls:* 键与本地回退默认集中一处）
|   |   +-- PanelPerms.cs              — 视图侧权限判定 helper（Has）
|   |   +-- MerchantLabels.cs          — 商家标注辅助
|   |
|   +-- Models/
|   |   +-- DTOs/                      — 代理 API 的响应 DTO
|   |   +-- ViewModels/                — 视图模型
|   |   +-- Enums/AdminAction.cs       — 审计动作枚举
|   |
|   +-- Services/
|   |   +-- AdminTokenService.cs       — 令牌刷新 + 按 device_id 缓存 + Cookie 回写
|   |   +-- BearerTokenHandler.cs      — 令牌注入 + 后端异常合成 502
|   |   +-- PriceConfigService.cs      — 价格配置缓存（Singleton）
|   |   +-- ServiceHealthService.cs    — 内置 Api/Identity 健康检查，扩展模块可追加
|   |
|   +-- ViewComponents/
|   |   +-- ServiceStatusViewComponent.cs — 服务状态表格骨架
|   |   +-- ProSlotViewComponent.cs       — 通用扩展槽（渲染 Views/Ext/{key}.cshtml，缺省输出空）
|   |
|   +-- Constants/TenantConstants.cs   — 租户 Realm 常量
|   |
|   +-- Views/                         — Razor 视图（Shared + 各控制器对应目录）
|   +-- wwwroot/
|       +-- css/site.css               — 主题 + 业务组件样式
|       +-- js/site.js, js/dialogs.js  — 侧栏折叠/主题切换/服务状态与统一弹窗
|       +-- lib/                       — Bootstrap 5 + jQuery + Font Awesome 6
|
+-- doc/                               — 设计文档
+-- deploy/                            — K8s 部署清单模板（deployment.yaml / ingress.yaml / secrets / README.md）
+-- .github/workflows/build.yml        — 构建工作流
+-- OpenFindBearings.Admin.slnx        — 解决方案
```

## 无本地数据库（权限与角色由 API 托管）

Admin 不直连任何数据库：仓库内无 `DbContext`、无 `DbSet`、无 EF 迁移目录、无实体目录、无本地种子数据，`appsettings.json` 亦无连接串。

早期版本曾设计 `db_admin` 四表（`admin_audit_logs`、`admin_user_roles`、`admin_role_permissions`、`admin_configs`）与本地 `PermissionKey` 枚举（Bearing* 权限键），二者均已删除，`Models/Enums` 现仅保留 `AdminAction.cs`。

因此，面板角色与权限目录的唯一事实源是 API：

| 能力 | API 端点 |
|------|----------|
| 权限目录 | `GET /api/admin/permissions` |
| 角色列表 | `GET /api/admin/roles`、`GET /api/admin/roles/all` |
| 角色权限查询 | `GET /api/admin/roles/{id}/permissions` |
| 新建/删除角色 | `POST /api/admin/roles`、`DELETE /api/admin/roles/{id}` |
| 分配角色权限 | `POST /api/admin/roles/{id}/permissions` |
| 用户在租户内的角色 | `GET /api/admin/users/by-auth/{sub}/roles`（写：`POST`/`DELETE`） |
| 当前用户权限 | `GET /api/me/permissions` |

## 面板角色与权限判定

面板角色固定三档，定义于 `Authorization/PanelRoles.cs`：`Admin`（超管）、`Operator`（运营）、`Auditor`（审核）。三者与 API RBAC 中的同名角色对应。

权限判定分两处，口径一致（`Authorization/PanelPermission.cs` 与 `Helpers/PanelPerms.cs`）：

- 控制器与页面用 `[PanelPermission("key")]` 特性，策略名 `Panel:key` 由动态策略提供器按需生成。
- 视图按钮级显隐用 `PanelPerms.Has(User, "key")`。
- 两处均以 `panel_role == Admin` 短路（超管全通过，避免漏配锁死），否则 `permission` 多值 claim 任一命中即通过。

登录时（`Controllers/AccountController.cs`）向 API 请求 `GET /api/me/permissions`：若用户角色与面板角色全集无交集则拒登；否则把角色写入 `panel_role` claim、权限写入 `permission` claim。之后每 30 分钟由 Cookie 事件（`Program.cs`）重拉 `/api/me/permissions` 复核，角色被收回即 `RejectPrincipal` 踢出，API 不可达时放行（避免抖动导致集体掉线），下轮重试。

## 权限目录

权限目录真相源为 API（`OpenFindBearings.Api` 基础设施层种子数据），经 `GET /api/admin/permissions` 暴露；Admin 的权限管理页只读展示并分组。目录为三段式「资源.动作」命名，共 32 个权限键：

| 分组 | 权限键 |
|------|--------|
| 仪表盘 | `dashboard.view` |
| 数据管理 | `bearing.view` `bearing.create` `bearing.edit` `bearing.delete` `brand.view` `brand.create` `brand.edit` `brand.delete` `type.view` `type.create` `type.edit` `type.delete` |
| 商家管理 | `merchant.view` `merchant.manage` `merchant.verify` `merchant.detach` |
| 数据危险操作 | `data.restore` `data.harddelete` |
| 审核管理 | `correction.review` `sourcing.view` `sourcing.manage` |
| 认证管理 | `user.view` `user.ban` `user.manage` `user.assign` `role.manage` `permission.view` |
| 系统配置 | `system.view` `system.manage` `points.manage` |
| 审计日志 | `audit.view` |

预置角色与其权限点数量（数量随 API 种子数据而定，以 API 为准）：`Admin` 拥有全部 32 个权限键；`Operator` 为日常运营集（仪表盘、数据管理 view/create/edit、审核组、寻货治理、用户封禁，刻意不含删除/恢复/彻底删除/解除归属/角色分配）；`Auditor` 为只读监察集（各 view 键加纠错审核与审计日志）。

## 侧边栏导航

菜单按登录时写入的 `permission` 多值 claim 动态渲染，`panel_role == Admin` 全显示（`Views/Shared/_Layout.cshtml`）：

```
OpenFindBearings
+-- 仪表盘                          dashboard.view
+-- 信息管理
|   +-- 基础信息
|   |   +-- 品牌列表                brand.view     (Data/Brands)
|   |   +-- 类型列表                type.view      (Data/BearingTypes)
|   |   +-- 轴承列表                bearing.view   (Data/Bearings)
|   |   +-- 替代品管理              merchant.verify (Interchanges/Index)
|   |   +-- 商家列表                merchant.view  (Data/Merchants)
+-- 审核管理
|   +-- 信息纠错审核                correction.review (Correction/Index)
|   +-- 入驻申请审批                merchant.verify   (MerchantVerify/Index)
|   +-- 证照材料审核                merchant.verify   (Document/Index)
|   +-- 商品审核                    merchant.verify   (MerchantBearingReview/Index)
|   +-- 寻货管理                    sourcing.view     (Sourcing/Index)
+-- 认证管理
|   +-- 用户管理                    user.view       (Users/Index)
|   +-- 角色管理                    role.manage     (Role/Index)
|   +-- 权限管理                    permission.view (Permission/Index)
+-- 勋章管理                        system.view     (Achievement/Index)
+-- 商城管理                        system.view     (Mall/Index)
+-- 审计日志
|   +-- 管理员日志                  audit.view      (AuditLog?source=identity)
|   +-- API 日志                    audit.view      (AuditLog?source=api)
+-- 系统配置                        system.view     (Config/Index)
```

信息管理分组与审核管理分组的显示条件分别是组内任一条目权限命中。扩展模块声明的菜单项若分组键为「审核」则并入审核管理分组，否则追加为顶层项或模块自建分组。

## 通用扩展点

基座只识别「模块」这一通用概念、不含具体业务，扩展方在同项目内新增实现即被自动接入（`Ext/AdminModuleLoader.cs` 反射发现并按启用开关过滤）：

- `IAdminModule`（`Ext/AdminExt.cs`）：声明服务注册、专属路由、健康检查项、侧边栏菜单、仪表盘 JSON 富化、审计日志源加载。
- `ProSlotViewComponent`（`ViewComponents/ProSlotViewComponent.cs`）：共享视图留一行通用槽调用，按 key 渲染 `Views/Ext/{key}.cshtml`，分部不存在则输出空串。
- 可选覆盖配置 `appsettings.pro.json`（`Program.cs`）：开源无此文件即无副作用，扩展模块可借此读取自身配置。

开源基座内无任何模块实现，以上均为空操作。

## 健康检查

`Services/ServiceHealthService.cs` 内置检查项为 Api 与 Identity 两项，并行探测各自 `/health/live`，状态存于内存；`/Home/Status` 返回 JSON 供前端渲染，登录页与仪表盘共用同一视图组件骨架。扩展模块可经 `IAdminModule.AddHealthServices` 追加检查项，开源无模块即仅此两项。

## 主题系统

亮色/暗色主题切换，基于 Bootstrap 5.3 的 `data-bs-theme` 属性实现，用户选择通过 `localStorage` 持久化。

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始设计文档 |
| v1.1.0 | 2026-06-08 | 新增 db_admin 数据库设计；补充审计策略；更新数据访问原则 |
| v1.1.1 | 2026-06-09 | 更新导航结构图；优化侧边栏分类 |
| v1.2.0 | 2026-06-11 | 对齐 Phase 1 实际实现：移除未实现内容；更新项目结构为实际文件布局；新增主题系统文档 |
| v1.3.0 | 2026-06-11 | db_admin 数据库（EF Core + Npgsql）；RBAC 3 角色 17 权限；DataController 20 个 CRUD Action；完整 CRUD + 软删除/恢复/彻底删除；权限键统一为 Bearing* |
| v1.4.0 | 2026-06-15 | BearerTokenHandler JWT 注入；Dashboard 统一端点；API SeedData 补 admin 权限 |
| v1.5.0 | 2026-07-06 | 侧边栏新增映射信息/任务管理/同步数据审核；ReviewController 审计日志四源聚合；健康检查路径改为 /health/live；PermissionKey 补充至 21 个 |
| v1.6.0 | 2026-08-03 | 爬虫管理降级为只读展示：移除 CrawlerController、Crawler 健康检测、Crawler 审计日志源、crawler.manage 权限；健康检查收敛为 3 服务；数据访问原则改为经 Sync 读取数据源清单；项目结构/导航/权限键同步 |
| v1.7.0 | 2026-09-11 | 代码审查对齐：(1) DTO 数量 13+→16；(2) ViewModel 数量 5→10；(3) Services 补充 AdminTokenService/PriceConfigService；(4) Views 补充 Role/Index+Permissions、Account/Profile、Data/ImportInventory；(5) Controllers 补充 RoleController（非 RolesController）、AccountController.Profile+SignoutCallbackOidc；(6) deploy 路径修正为项目根目录；(7) 补充 ingress.yaml/secret.yml 等部署文件；(8) 数据访问原则中纠错/营业执照/商家认证端点路径修正为 /api/admin/*；(9) 权限键 v1.5.0 changelog 中 21 个修正为 17 个 |
| v1.7.1 | 2026-10-18 | 去除已移除的 Sync 集成残留：(1) 概述与架构总览删除 Sync 后端服务；(2) 数据访问原则删除同步数据审核/ETL 触发/映射管理/爬虫数据源清单四行；(3) 项目结构移除 SyncController/MappingController/ReviewController 及 Sync/Mapping/Review/Crawler 视图，并去掉库存导入（ImportInventory）；(4) 侧边栏删除映射信息、同步数据审核、任务管理及审计日志 Sync 源；(5) 权限键清单删除 etl.manage（17→16）；(6) 健康检查由 3 服务收敛为 2 服务（Api/Identity） |
| v1.7.2 | 2026-10-18 | 全量对齐代码事实：(1) 删除「Admin 拥有独立 db_admin 数据库」及整个「数据库设计（db_admin）」章节，改为「无本地数据库（权限与角色由 API 托管）」，说明无 DbContext/迁移/实体/种子、无连接串，早期四表与本地 PermissionKey 枚举均已删；(2) 权限键清单按 API 目录校准为 32 键（原 16 键），标注真相源与访问端点；(3) RBAC 章节改为面板角色三档（Admin/Operator/Auditor）+ 面板角色与权限判定；(4) 项目结构按真实目录重写（新增 Authorization/Ext/Helpers/ViewComponents，删除已不存在的 Controllers/Views/Data/Models.Entities）；(5) LicenseController/Views/License 更正为 DocumentController/证照材料审核；(6) deploy/k3s/ 更正为仓库根 deploy/ 实际文件；(7) 侧边栏按当前 _Layout.cshtml 重写（补齐替代品管理/寻货管理/勋章管理/商城管理，命名对齐）；(8) 健康检查保持 2 服务并说明扩展项；(9) 新增通用扩展点章节；(10) 删除无从核实或已失效的 DTO/ViewModel/Entity 计数与 DataController Action 计数说法 |
