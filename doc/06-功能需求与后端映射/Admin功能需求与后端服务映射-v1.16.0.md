# Admin 功能需求与后端服务映射 v1.16.0

## 概述

本文档整理 Admin 后台全部功能需求，逐一标注实际实现状态。✅ = 已实现，❌ = 待实现。

## 变更日志

### v1.14.0 → v1.15.0 更新内容

1. **轴承列表在售商家反向查询**：API 新增 `GET /api/bearings/{id:guid}/merchants`（按轴承反查销售该轴承的商家，支持 onlyOnSale 与分页）；Admin 新增代理端点 `GET /api/proxy/bearing-merchants/{id}` 及轴承列表页"在售商家"按钮与弹窗；点击商家行跳转 `/Data/Merchants?merchantId=...&search=...` 并自动打开该商家"在售商品"弹窗（复用现有弹窗），无需新建独立商家详情页。

2. **基础信息列表头部响应式适配**：品牌/类型/轴承列表的 card-header 与搜索表单改为 `flex-wrap`，手机屏不再溢出；"新建"按钮文本在小屏（<sm）隐藏，仅保留加号图标。

### v1.15.0 → v1.16.0 更新内容

1. **任务管理页增强（运行态 + 最近执行）**：任务管理页（Sync/Index）改造为展示四张状态卡片（Extract/Transform/Load/爬虫最近执行），各阶段卡片显示"进行中/排队中"或"空闲"及最近状态；新增"最近执行记录"表格（阶段/状态/开始/结束/成功/失败/跳过/错误信息），支持按阶段筛选与手动刷新；页面每 15 秒经代理端点轮询刷新。手动触发按钮在对应阶段存在活跃任务（Pending 或 Running，含定时调度 CronJob 创建的任务）时自动禁用，并提示"该阶段当前有进行中或排队中的任务（可能来自定时调度），暂不能手动执行"。

2. **新增 ETL 代理与后端端点**：Admin 新增代理端点 `GET /api/proxy/etl/summary`、`GET /api/proxy/etl/tasks`；Sync API 新增 `GET /api/etl/summary`（各阶段运行态 + 最近任务 + 爬虫最近执行情况聚合，爬虫数据由 CbiaDbContext 聚合 db_raw 的 cbia_* 表 sync_status 分布与最新 crawled_at，不新增爬虫落库表）、`GET /api/etl/tasks`（ETL 任务历史分页，支持 CommandType/Status 过滤）。手动触发端点（/run、/extract、/transform、/load）新增冲突守卫：对应阶段存在 Pending 或 Running 任务时返回 409，避免与每日 04:00-07:00 UTC 定时调度冲突。

### v1.13.0 → v1.14.0 更新内容

1. **Dashboard 商家统计三分类**：首行商家总数卡内嵌"已入驻 X" 格式（如 0/690），第二行第 4 卡由"认证商家数量"改为"入驻申请待审批"，不统计爬虫商家数量。卡片文字变更：h3 显示 0/690（已入驻/总数），标题改"商家数量（已入驻/总数）"、移除原内嵌小字。

2. **四审核界面统一 Review 风格**：将审核管理名称与 dashboard 卡片统一并统一四个审核界面为 Review 风格：page-header、nav-tabs 状态筛选、card 内 table-hover 表格、空状态居中图标、btn-success 通过 / btn-outline-danger 拒绝、card-footer 分页。Controller 层零改动（License 表单 POST 调 Approve/Reject 保留，三个 Controller 已有全部所需 ViewBag）。

3. **审核管理命名统一**：使用"入驻申请审批"替代"商家认证审核"；菜单与 dashboard 卡片统一为"同步数据审核/信息纠错审核/营业执照审核/入驻申请审批"，名称不含"待"字。

4. **入驻申请审批列表数据源**：MerchantVerifyController 调 GET /api/admin/merchants 恒带 excludeCrawler=true 过滤爬虫商家；statusMap 为 pending=2/active=0/suspended=1，默认 tab 为"待认证"（status=pending）。入驻申请审批只看未认证商家，不加全部/已认证筛选。

5. **商家管理页筛选与排序**：/Data/Merchants 页面支持按 IsVerified 筛选：tabs 为全部/已认证/未认证，透传 API 的 `verifiedOnly` 参数；排序为 OrderByDescending(IsVerified).ThenBy(Name)（已认证优先，同级按名称）。

6. **编辑式审核流程**：Admin 审核页"通过"按钮打开编辑弹窗，经 GET /api/audit/{auditId:guid} 预填 staging 当前字段值，提交 = ApproveAuditCommand.Fields 字典写字段 + ReadyToSync + IsManuallyApproved=true；字段经 AuditFieldMapper 按 EntityType 白名单双向映射，轴承 Dimensions/Performance/Price 值对象任一子字段出现即整体重建。E 阶段轴承/商家/替代品三处 UpdateFrom 前置 IsManuallyApproved 跳过，与 T 阶段 TransformService 对 IsManuallyApproved 记录直接 MarkAsReadyToSync 跳过重评分，共同保证人工记录不被自动任务覆盖。

7. **L阶段不再清空爬虫数据**：商家/关联数据不再受 L阶段直接清理影响，爬虫数据保留在 staging 表中作为备用，商家审核通过不自动清空源数据。

## 功能总览

### 第一层：仪表盘（首页 Dashboard）

| 功能点 | 数据源 | 端点 | 实现状态 |
|--------|--------|------|----------|
| 4 个后端服务在线状态 | 各服务 /health/live | ServiceHealthService 并行 AJAX | ✅ |
| 轴承总量 | API | `GET /api/admin/dashboard/stats` → Bearings.TotalCount | ✅ |
| 品牌总量 | API | → Brands.TotalCount | ✅ |
| 商家总量 | API | → Merchants.TotalCount（卡片内嵌显示"已入驻 X"） | ✅ |
| 类型总量 | API | → BearingTypes.TotalCount | ✅ |
| 同步数据审核数 | Sync API | `GET /api/audit/stats` → totalPending | ✅ |
| 信息纠错审核数 | API | `GET /api/admin/dashboard/stats` → Corrections.PendingCount | ✅ |
| 营业执照审核数 | API | → Licenses.PendingCount | ✅ |
| 入驻申请审批数 | API | → Merchants.PendingApplicationCount | ✅ |

### 第二层：信息管理

#### 基础信息

| 功能点 | 后端 | 端点 | 实现状态 |
|--------|------|------|----------|
| 品牌列表（分页+搜索） | API | `GET /api/admin/brands?page=&search=&includeDeleted=` | ✅ |
| 品牌新建 | API | `POST /api/admin/brands` | ✅ |
| 品牌编辑 | API | `PUT /api/admin/brands/{id}` | ✅ |
| 品牌删除 | API | `DELETE /api/admin/brands/{id}`（软删除） | ✅ |
| 品牌恢复 | API | `PUT /api/admin/brands/{id}/restore` | ✅ |
| 品牌彻底删除 | API | `DELETE /api/admin/brands/{id}/hard`（限 admin 角色） | ✅ |
| 类型列表（分页+搜索） | API | `GET /api/admin/bearing-types?page=&search=&includeDeleted=` | ✅ |
| 类型新建 | API | `POST /api/admin/bearing-types` | ✅ |
| 类型编辑 | API | `PUT /api/admin/bearing-types/{id}` | ✅ |
| 类型删除 | API | `DELETE /api/admin/bearing-types/{id}`（软删除） | ✅ |
| 类型恢复 | API | `PUT /api/admin/bearing-types/{id}/restore` | ✅ |
| 类型彻底删除 | API | `DELETE /api/admin/bearing-types/{id}/hard`（限 admin 角色） | ✅ |
| 轴承列表（分页+搜索+过滤） | API | `GET /api/bearings/search?page=&search=&brand=&type=&includeDeleted=` | ✅ |
| 轴承新建 | API | `POST /api/admin/bearings` | ✅ |
| 轴承编辑 | API | `PUT /api/admin/bearings/{id}` | ✅ |
| 轴承删除 | API | `DELETE /api/admin/bearings/{id}`（软删除） | ✅ |
| 轴承恢复 | API | `PUT /api/admin/bearings/{id}/restore` | ✅ |
| 替代品弹窗 | API | `GET /api/proxy/interchanges/{id}` | ✅ |
| 在售商家弹窗 | API | `GET /api/proxy/bearing-merchants/{id}` | ✅ |
| 商家列表（分页+搜索） | API | `GET /api/merchants?page=&search=&includeDeleted=` | ✅ |
| 商家新建 | API | `POST /api/admin/merchants` | ✅ |
| 商家编辑 | API | `PUT /api/admin/merchants/{id}` | ✅ |
| 商家删除 | API | `DELETE /api/admin/merchants/{id}`（软删除） | ✅ |
| 商家恢复 | API | `PUT /api/admin/merchants/{id}/restore` | ✅ |
| 在售商品弹窗 | API | `GET /api/proxy/merchant-bearings/{id}` | ✅ |
| Excel 导入在售轴承 | Sync API | `POST /api/proxy/excel/import-bearing`（Admin 中转） | ✅ |
| 下载导入模板 | Sync API | `GET /api/proxy/excel/template` | ✅ |
| 显示已删除项 | 视图 | `?includeDeleted=true` | ✅ |

#### 库存管理

| 功能点 | 后端 | 端点 | 实现状态 |
|--------|------|------|----------|
| Excel 导入商家库存 | Sync API | `POST /api/inventory/import?merchantId=`（Admin 中转调用 SyncClient） | ✅ |
| 下载库存导入模板 | Sync API | `GET /api/inventory/template` | ✅ |

#### 映射信息

| 功能点 | 后端 | 端点 | 实现状态 |
|--------|------|------|----------|
| 品牌映射列表 | Sync API | `GET /api/config/brands` | ✅ |
| 品牌映射新建 | Sync API | `POST /api/config/brands` | ✅ |
| 品牌映射编辑 | Sync API | `PUT /api/config/brands/{id}` | ✅ |
| 品牌映射删除 | Sync API | `DELETE /api/config/brands/{id}` | ✅ |
| 类型映射列表 | Sync API | `GET /api/config/types` | ✅ |
| 类型映射新建 | Sync API | `POST /api/config/types` | ✅ |
| 类型映射编辑 | Sync API | `PUT /api/config/types/{id}` | ✅ |
| 类型映射删除 | Sync API | `DELETE /api/config/types/{id}` | ✅ |

#### 审核管理

| 功能点 | 后端 | 端点 | 实现状态 |
|--------|------|------|----------|
| 同步数据审核列表 | Sync API | `GET /api/audit/pending?entityType=` | ✅ |
| 同步数据审核通过（编辑弹窗） | Sync API | `GET /api/audit/{id}` 拉取可编辑字段预填 + `POST /api/audit/{id}/approve`（JSON 携带 fields 字典） | ✅ |
| 同步数据审核拒绝 | Sync API | `POST /api/audit/{id}/reject` | ✅ |
| 信息纠错列表 | API | `GET /api/admin/corrections` | ✅ |
| 信息纠错通过 | API | `POST /api/admin/corrections/{id}/approve` | ✅ |
| 信息纠错拒绝 | API | `POST /api/admin/corrections/{id}/reject` | ✅ |
| 营业执照列表 | API | `GET /api/admin/licenses/pending` | ✅ |
| 营业执照审核通过 | API | `POST /api/admin/licenses/{id}/approve` | ✅ |
| 营业执照审核拒绝 | API | `POST /api/admin/licenses/{id}/reject` | ✅ |
| 入驻申请列表（排除爬虫来源） | API | `GET /api/admin/merchants?excludeCrawler=true&status=`（status：2=待认证、0=已认证、1=已拒绝） | ✅ |
| 入驻申请认证 | API | `POST /api/admin/merchants/{id}/verify`（不再清除爬虫数据） | ✅ |
| 查看同步数据 | API | 弹窗调用 `GET /api/merchants/{id}/bearings?dataSource=Crawler`，支持"显示全部"切换 | ✅ |

### 第三层：任务管理

#### ETL 同步

| 功能点 | 后端 | 端点 | 实现状态 |
|--------|------|------|----------|
| 触发全量 ETL | Sync API | `POST /api/etl/run` | ✅ |
| 触发 E 阶段 | Sync API | `POST /api/etl/extract` | ✅ |
| 触发 T 阶段 | Sync API | `POST /api/etl/transform` | ✅ |
| 触发 L 阶段 | Sync API | `POST /api/etl/load` | ✅ |
| 触发单个阶段（参数控制） | Sync API | `POST /api/etl/run?phases=extract,transform,load` | ✅ |
| 各阶段运行态展示 | Sync API | `GET /api/proxy/etl/summary`（聚合 `/api/etl/summary`） | ✅ |
| 最近执行记录（分页/筛选） | Sync API | `GET /api/proxy/etl/tasks`（聚合 `/api/etl/tasks`） | ✅ |
| 手动触发冲突守卫 | Sync API | `/run`、`/extract`、`/transform`、`/load` 在对应阶段存在 Pending/Running 任务时返回 409 | ✅ |

#### 数据爬虫

| 功能点 | 后端 | 端点 | 实现状态 |
|--------|------|------|----------|
| 爬虫数据源清单（只读） | Sync API | `GET /api/datasources` | ✅ |
| 调度策略展示（静态文案） | — | — | ✅ |
| 手动触发爬取 | — | — | ❌ 不支持（Crawler 纯 CronJob） |
| 运行历史 | — | — | ❌ 不保留（K8s CronJob 原生保留 3 次） |

### 第四层：认证管理

| 功能点 | 后端 | 端点 | 实现状态 |
|--------|------|------|----------|
| 用户列表 | Identity | `GET /api/account/admin/users?includeDeleted=` | ✅ |
| 用户删除（软删除） | Identity | `DELETE /api/account/admin/users/{id}` | ✅ |
| 用户恢复 | Identity | `POST /api/account/admin/users/{id}/restore` | ✅ |
| 用户彻底删除 | Identity | `DELETE /api/account/admin/users/{id}/permanent`（仅限已软删除用户，禁止删除当前管理员本人） | ✅ |
| 启用/禁用 | Identity | `PATCH /api/account/admin/users/{id}/status` | ✅ |
| 重置密码 | Identity | `POST /api/account/admin/users/{id}/reset-password` | ✅ |
| 解锁用户 | Identity | `POST /api/account/admin/users/{id}/unlock` | ✅ |
| 权限管理 | db_admin | RBAC 角色权限映射 | ✅ |
| 审计日志（管理员） | db_admin | Admin 本地 admin_audit_logs | ✅ |
| 审计日志（API） | API | `GET /api/audit/logs?source=api` | ✅ |
| 审计日志（Sync） | Sync API | `GET /api/audit-log?source=sync` | ✅ |

### 第五层：系统配置

| 功能点 | 后端 | 端点 | 实现状态 |
|--------|------|------|----------|
| 配置列表 | API | `GET /api/admin/config` | ✅ |
| 编辑配置 | API | `PUT /api/admin/config/{key}` | ✅ |

## 控制器清单（12 个）

| 控制器 | 职责 | 调用服务 |
|--------|------|---------|
| AccountController | 登录/回调/登出/修改密码 | Identity OAuth |
| HomeController | 仪表盘 + /Home/Status + /Home/DataSources | ServiceHealthService / Sync API |
| DataController | 轴承/品牌/类型/商家 CRUD（24 个 Action） | API |
| SyncController | ETL 控制面板 | Sync API |
| MappingController | 品牌/类型映射 CRUD | Sync API |
| ReviewController | 同步数据审核（编辑弹窗 + 通过/拒绝） | Sync API |
| CorrectionController | 信息纠错审核 | API |
| LicenseController | 营业执照审核 | API |
| MerchantVerifyController | 入驻申请审批（不再清除爬虫数据）+ 同步数据弹窗 | API |
| UsersController | 用户管理（软删除/恢复/彻底删除/启用禁用/重置密码） | Identity |
| PermissionController | 权限管理（db_admin RBAC） | db_admin |
| AuditLogController | 审计日志（三服务 tab 切换） | API/Sync/Admin |

## 基础设施

| 组件 | 说明 |
|------|------|
| Program.cs | 3 命名 HttpClient（ApiClient、SyncClient 带 BearerTokenHandler）+ ServiceHealthService + 代理端点（支持 dataSource 和 onlyOnSale 参数；新增 bearing-merchants 反向查询代理） |
| ApplicationDbContext | EF Core DbContext（4 个 DbSet）+ PostgreSQL |
| SeedData | 3 角色（admin/editor/viewer）+ 17 权限键种子数据 |
| BearerTokenHandler | 从 cookie 自动提取 JWT 注入 ApiClient 请求头 |
| ServiceHealthService | 并行检查 3 服务 /health/live 端点 |
| _Layout.cshtml | 固定侧边栏 + 顶栏 + 主题切换 + 用户图标右侧 + CDN 回退 |
| site.css | 亮色/暗色主题 CSS 变量 |
| site.js | 侧边栏折叠 + 主题切换 + 服务状态 AJAX |

## 待实现

| 功能 | 优先级 | 依赖 |
|------|--------|------|
| ETL 任务历史列表 | 低 | 已完成（Sync API 新增 /api/etl/tasks + /api/etl/summary） |

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始版本 |
| v1.1.0 | 2026-06-11 | 对齐 Phase 1：标注已实现/待实现；新增控制器/视图/基础设施清单 |
| v1.2.0 | 2026-06-11 | 新增品牌/类型/轴承/商家完整 CRUD；Excel 导入；includeDeleted 复选框；DataController 从列表扩展到 20 个 Action |
| v1.3.0 | 2026-06-15 | Dashboard 统一端点；待审核数从 ❌ 改为 ✅；新增 BearerTokenHandler 基础设施 |
| v1.4.0 | 2026-07-06 | 新增 ReviewController 同步数据审核；AuditLogController 从单源改为四源聚合；MappingController 从只读改为 CRUD；侧边栏菜单重新组织（任务管理/映射信息）；健康检查路径统一为 /health/live；PermissionKey 更新 |
| v1.5.0 | 2026-07-07 | 信息管理新增"库存管理"子菜单（库存导入页面），侧边栏新增导入入口 |
| v1.6.0 | 2026-07-07 | 库存导入改为表单提交经 Admin Controller 代理；新增 DownloadInventoryTemplate 代理 Action，模板下载不再直接暴露 Sync API 地址 |
| v1.7.0 | 2026-07-07 | 修正配置端点路径 /api/config → /api/admin/config；补齐 data.harddelete 权限映射 |
| v1.8.0 | 2026-07-09 | 商家认证审核不再清除爬虫数据；新增"查看同步数据"弹窗（调用 GET /api/merchants/{id}/bearings?dataSource=Crawler）；代理端点支持 dataSource 和 onlyOnSale 参数；MerchantVerifyController 职责同步更新 |
| v1.9.0 | 2026-08-03 | 爬虫管理降级为只读展示：数据爬虫功能改为经 Sync /api/datasources 动态读取数据源清单 + 静态调度策略；移除 Crawler API 调用、CrawlerController、Crawler 审计日志源；控制器清单 13→12、权限键 18→17；健康检测收敛为 3 服务 |
| v1.10.0 | 2026-08-12 | 同步数据审核升级为编辑式审核：通过按钮打开编辑弹窗，经 GET /api/audit/{id} 拉取 Staging 实体可编辑字段预填，提交时以 JSON 携带 fields 字典（ReviewApproveRequest）调用 POST /api/audit/{id}/approve；新增 ReviewController.GetDetail action 透传详情；ReviewController.Approve 由 FormUrlEncoded 改为 JSON |
| v1.11.0 | 2026-08-19 | 仪表盘商家卡片归类：商家总量卡内嵌显示"已入驻 X"（Merchants.VerifiedCount）；"商家认证待审核数"改为"入驻申请待审批数"（Merchants.PendingApplicationCount，统计待审核且非爬虫来源的商家）；删除 API SeedData 演示商家相关数据 |
| v1.12.0 | 2026-08-20 | 审核管理菜单与仪表盘卡片名称统一（去"待"字）：同步数据审核 / 信息纠错审核 / 营业执照审核 / 入驻申请审批（原"商家认证审核"）；四个审核界面统一样式为同步数据审核风格（page-header + nav-tabs 状态筛选 + 统一表格 / 操作按钮 / 分页 / 空状态），Correction 与 License 新增分页；入驻申请审批列表改为仅显示非爬虫来源商家（GET /api/admin/merchants?excludeCrawler=true），修正状态筛选映射（2=待认证/0=已认证/1=已拒绝） |
| v1.13.0 | 2026-08-20 | 用户管理支持彻底删除与显示已删除：新增"显示已删除"复选框（includeDeleted=true）与已删除用户"恢复/彻底删除"按钮（Identity 新增 DELETE /api/account/admin/users/{id}/permanent，仅限已软删除用户，禁止删除当前管理员本人）；用户/角色管理列表样式统一为品牌列表风格（card-header 计数 + btn-success 新建 + 表格 + 智能分页） |
| v1.15.0 | 2026-08-29 | 轴承列表新增"在售商家"反查（API 新增 GET /api/bearings/{id}/merchants、Admin 新增代理与按钮弹窗，点击商家跳转商家管理页并自动打开在售商品弹窗）；基础信息列表头部响应式适配，新建按钮小屏仅显示加号图标 |
| v1.16.0 | 2026-08-29 | 任务管理页增强：状态卡片展示各阶段运行态（进行中/排队中/空闲）与爬虫最近执行情况，最近执行记录表格支持按阶段筛选与刷新；手动触发按钮在阶段存在活跃任务时禁用并提示可能与定时调度冲突；Admin 新增代理 GET /api/proxy/etl/summary、GET /api/proxy/etl/tasks，Sync API 新增 GET /api/etl/summary（运行态+最近任务+爬虫聚合）、GET /api/etl/tasks（历史分页），手动触发端点新增 409 冲突守卫 |
