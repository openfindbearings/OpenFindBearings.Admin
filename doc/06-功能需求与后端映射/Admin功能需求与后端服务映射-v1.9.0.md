# Admin 功能需求与后端服务映射 v1.9.0

## 概述

本文档整理 Admin 后台全部功能需求，逐一标注实际实现状态。✅ = 已实现，❌ = 待实现。

## 功能总览

### 第一层：仪表盘（首页 Dashboard）

| 功能点 | 数据源 | 端点 | 实现状态 |
|--------|--------|------|----------|
| 4 个后端服务在线状态 | 各服务 /health/live | ServiceHealthService 并行 AJAX | ✅ |
| 轴承总量 | API | `GET /api/admin/dashboard/stats` → Bearings.TotalCount | ✅ |
| 品牌总量 | API | → Brands.TotalCount | ✅ |
| 商家总量 | API | → Merchants.TotalCount | ✅ |
| 类型总量 | API | → BearingTypes.TotalCount | ✅ |
| 同步数据待审核数 | Sync API | `GET /api/audit/stats` → totalPending | ✅ |
| 纠错待审核数 | API | `GET /api/admin/dashboard/stats` → Corrections.PendingCount | ✅ |
| 营业执照待审核数 | API | → Licenses.PendingCount | ✅ |
| 商家认证待审核数 | API | → Merchants.PendingVerificationCount | ✅ |

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
| 同步数据审核通过 | Sync API | `POST /api/audit/{id}/approve` | ✅ |
| 同步数据审核拒绝 | Sync API | `POST /api/audit/{id}/reject` | ✅ |
| 信息纠错列表 | API | `GET /api/admin/corrections` | ✅ |
| 信息纠错通过 | API | `POST /api/admin/corrections/{id}/approve` | ✅ |
| 信息纠错拒绝 | API | `POST /api/admin/corrections/{id}/reject` | ✅ |
| 营业执照列表 | API | `GET /api/admin/licenses/pending` | ✅ |
| 营业执照审核通过 | API | `POST /api/admin/licenses/{id}/approve` | ✅ |
| 营业执照审核拒绝 | API | `POST /api/admin/licenses/{id}/reject` | ✅ |
| 商家认证搜索 | API | `GET /api/merchants/search?unverified=true` | ✅ |
| 商家认证审核 | API | `POST /api/admin/merchants/{id}/verify`（不再清除爬虫数据） | ✅ |
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
| ETL 任务历史 | — | — | ❌ |

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
| 用户列表 | Identity | `GET /api/account/admin/users` | ✅ |
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
| ReviewController | 同步数据审核 | Sync API |
| CorrectionController | 信息纠错审核 | API |
| LicenseController | 营业执照审核 | API |
| MerchantVerifyController | 商家认证审核（不再清除爬虫数据）+ 同步数据弹窗 | API |
| UsersController | 用户管理 | Identity |
| PermissionController | 权限管理（db_admin RBAC） | db_admin |
| AuditLogController | 审计日志（三服务 tab 切换） | API/Sync/Admin |
| ConfigController | 配置管理 | API |

## 基础设施

| 组件 | 说明 |
|------|------|
| Program.cs | 3 命名 HttpClient（ApiClient、SyncClient 带 BearerTokenHandler）+ ServiceHealthService + 代理端点（支持 dataSource 和 onlyOnSale 参数） |
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
| ETL 任务历史列表 | 中 | Sync API 新端点 |

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
