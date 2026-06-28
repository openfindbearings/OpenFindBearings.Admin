# Admin 功能需求与后端服务映射 v1.3.0

## 概述

本文档整理 Admin 后台全部功能需求，逐一标注实际实现状态。Phase 1 已实现的标记为 ✅，待实现的标记为 ❌。

## 功能总览

### 第一层：仪表盘（首页 Dashboard）

| 功能点 | 数据源 | 端点 | Phase 1 状态 |
|--------|--------|------|-------------|
| Crawler 在线状态 | Crawler API | `GET /live` | ✅ 已实现 |
| Sync 在线状态 | Sync API | `GET /live` | ✅ 已实现 |
| API 在线状态 | API | `GET /live` | ✅ 已实现 |
| Identity 在线状态 | Identity API | `GET /live` | ✅ 已实现 |
| 轴承总量 | API | `GET /api/admin/dashboard/stats` → Bearings.TotalCount | ✅ 已实现 |
| 品牌总量 | API | `GET /api/admin/dashboard/stats` → Brands.TopBrands.Length | ✅ 已实现 |
| 商家总量 | API | `GET /api/admin/dashboard/stats` → Merchants.TotalCount | ✅ 已实现 |
| 待审核数 | API | `GET /api/admin/dashboard/stats` → Corrections.PendingCount | ✅ 已实现 |

### 第二层：爬虫管理

| 功能点 | 后端 | 端点 | Phase 1 状态 |
|--------|------|------|-------------|
| 爬虫列表 | Crawler API | `GET /api/crawlers` | ✅ 已实现 |
| 爬虫状态 | Crawler API | `GET /api/crawlers/{name}/status` | ✅ 已实现 |
| 触发运行 | Crawler API | `POST /api/crawlers/{name}/run` | ✅ 已实现 |
| 运行历史 | — | — | ❌ 需 Crawler 或 Admin DB |

### 第三层：ETL 同步管理

| 功能点 | 后端 | 端点 | Phase 1 状态 |
|--------|------|------|-------------|
| 触发全量 ETL | Sync API | `POST /api/etl/run` | ✅ 已实现 |
| 触发 E 阶段 | Sync API | `POST /api/etl/extract` | ✅ 已实现 |
| 触发 T 阶段 | Sync API | `POST /api/etl/transform` | ✅ 已实现 |
| 触发 L 阶段 | Sync API | `POST /api/etl/load` | ✅ 已实现 |
| ETL 任务历史 | — | — | ❌ 需 Sync API 新端点 |

### 第四层：业务数据管理

| 功能点 | 后端 | 端点 | Phase 1 状态 |
|--------|------|------|-------------|
| 轴承列表 | API | `GET /api/bearings/search` | ✅ 已实现 |
| 轴承新建 | API | `POST /api/admin/bearings` | ✅ 已实现 |
| 轴承编辑 | API | `PUT /api/admin/bearings/{id}` | ✅ 已实现 |
| 轴承删除 | API | `DELETE /api/admin/bearings/{id}` | ✅ 已实现（软删除） |
| 轴承恢复 | API | `PUT /api/admin/bearings/{id}/restore` | ✅ 已实现 |
| 品牌列表 | API | `GET /api/brands` | ✅ 已实现 |
| 品牌新建 | API | `POST /api/admin/brands` | ✅ 已实现 |
| 品牌编辑 | API | `PUT /api/admin/brands/{id}` | ✅ 已实现 |
| 品牌删除 | API | `DELETE /api/admin/brands/{id}` | ✅ 已实现（软删除） |
| 品牌恢复 | API | `PUT /api/admin/brands/{id}/restore` | ✅ 已实现 |
| 品牌彻底删除 | API | `DELETE /api/admin/brands/{id}/hard` | ✅ 已实现 |
| 类型列表 | API | `GET /api/bearing-types` | ✅ 已实现 |
| 类型新建 | API | `POST /api/admin/bearing-types` | ✅ 已实现 |
| 类型编辑 | API | `PUT /api/admin/bearing-types/{id}` | ✅ 已实现 |
| 类型删除 | API | `DELETE /api/admin/bearing-types/{id}` | ✅ 已实现（软删除） |
| 类型恢复 | API | `PUT /api/admin/bearing-types/{id}/restore` | ✅ 已实现 |
| 类型彻底删除 | API | `DELETE /api/admin/bearing-types/{id}/hard` | ✅ 已实现 |
| 商家列表 | API | `GET /api/merchants` | ✅ 已实现 |
| 商家新建 | API | `POST /api/admin/merchants` | ✅ 已实现 |
| 商家编辑 | API | `PUT /api/admin/merchants/{id}` | ✅ 已实现 |
| 商家删除 | API | `DELETE /api/admin/merchants/{id}` | ✅ 已实现（软删除） |
| 商家恢复 | API | `PUT /api/admin/merchants/{id}/restore` | ✅ 已实现 |
| 替代品弹窗 | API | `GET /api/proxy/interchanges/{id}` | ✅ 已实现 |
| 在售商品弹窗 | API | `GET /api/proxy/merchant-bearings/{id}` | ✅ 已实现 |
| Excel 导入在售轴承 | Sync API | `POST /api/proxy/excel/import-bearing` | ✅ 已实现 |
| 下载导入模板 | Sync API | `GET /api/proxy/excel/template` | ✅ 已实现 |
| 显示已删除项 | 视图 | `?includeDeleted=true` | ✅ 已实现 |

### 第五层：审核管理

| 功能点 | 后端 | 端点 | Phase 1 状态 |
|--------|------|------|-------------|
| 信息纠错列表 | API | `GET /api/corrections` | ✅ 已实现 |
| 营业执照审核 | API | `GET /api/merchants/licenses` | ✅ 已实现 |
| 商家认证审核 | API | `POST /api/merchants/{id}/verify` | ✅ 已实现 |

### 第六层：认证管理

| 功能点 | 后端 | 端点 | Phase 1 状态 |
|--------|------|------|-------------|
| 用户列表 | Identity | `GET /api/account/admin/users` | ✅ 已实现 |
| 启用/禁用 | Identity | `POST /api/account/admin/users/{id}/toggle-status` | ✅ 已实现 |
| 权限管理 | db_admin | RBAC 角色权限映射 | ✅ 已实现 |
| 审计日志 | Identity | `GET /api/auditlog` | ✅ 已实现 |

### 第七层：系统配置

| 功能点 | 后端 | 端点 | Phase 1 状态 |
|--------|------|------|-------------|
| 配置列表 | API | `GET /api/config` | ✅ 已实现 |
| 编辑配置 | API | `PUT /api/config/{key}` | ✅ 已实现 |

## Phase 1 已实现清单

### 控制器（12 个）

| 控制器 | 职责 | 调用服务 |
|--------|------|---------|
| HomeController | 仪表盘 + /Home/Status | ServiceHealthService |
| DataController | 轴承/品牌/类型/商家 CRUD（20 个 Action） | API |
| CrawlerController | 爬虫列表 + 触发运行 | Crawler API |
| SyncController | ETL 控制面板 | Sync API |
| MappingController | 映射查看 | API |
| CorrectionController | 纠错审核 | API |
| LicenseController | 营业执照审核 | API |
| MerchantVerifyController | 商家认证 | API |
| UsersController | 用户管理 | Identity |
| PermissionController | 权限管理（db_admin RBAC） | db_admin |
| AuditLogController | 审计日志 | Identity |
| ConfigController | 配置管理 | API |

### 视图（16 个）

| 视图 | 功能 |
|------|------|
| Home/Index | 仪表盘（4 统计卡片 + 服务状态表） |
| Data/Bearings | 轴承列表 + 新建 + 搜索 + 分页 + 替代品弹窗 + 编辑 + 删除/恢复 |
| Data/Brands | 品牌列表 + 新建 + 搜索 + 分页 + 编辑 + 删除/恢复/彻底删除 + includeDeleted |
| Data/BearingTypes | 类型列表 + 新建 + 搜索 + 分页 + 编辑 + 删除/恢复/彻底删除 + includeDeleted |
| Data/Merchants | 商家列表 + 新建 + 搜索 + 分页 + 在售商品弹窗 + 编辑 + 删除/恢复 + Excel 导入 |
| Crawler/Index | 爬虫列表 + 触发运行 |
| Sync/Index | ETL 控制面板 + 调度说明 |
| Mapping/Index | 品牌/类型映射查看 |
| Correction/Index | 纠错审核 + 状态筛选 |
| License/Index | 营业执照审核 + 通过/拒绝 |
| MerchantVerify/Index | 商家认证 + 搜索 |
| Users/Index | 用户管理 + 启用/禁用 |
| Permission/Index | 权限管理（db_admin RBAC） |
| AuditLog/Index | 审计日志列表 |
| Config/Index | 配置管理 + 编辑弹窗 |

### 基础设施

| 组件 | 说明 |
|------|------|
| Program.cs | 4 命名 HttpClient（ApiClient 带 BearerTokenHandler）+ ServiceHealthService + 4 代理端点 |
| ApplicationDbContext | EF Core DbContext（4 个 DbSet）+ PostgreSQL |
| SeedData | 3 角色（admin/editor/viewer）+ 17 权限键种子数据 |
| BearerTokenHandler | 从 cookie 自动提取 JWT 注入 ApiClient 请求头 |
| ServiceHealthService | 并行检查 4 服务 /live 端点 |
| _Layout.cshtml | 固定侧边栏 + 顶栏 + 主题切换 + 用户图标右侧 + CDN 回退 |
| site.css | 亮色/暗色主题 CSS 变量 |
| site.js | 侧边栏折叠 + 主题切换 + 服务状态 AJAX + 弹窗交互 |

## Phase 2 待实现清单

| 功能 | 优先级 | 依赖 |
|------|--------|------|
| ETL 任务历史列表 | 中 | Sync API 新端点 |
| 爬虫运行历史 | 中 | Admin DB 或 Crawler API |
| 品牌/类型映射管理 | 中 | Sync API 映射端点 |

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始版本，完整的 Admin 功能与后端映射 |
| v1.1.0 | 2026-06-11 | 对齐 Phase 1 实际实现：标注已实现/待实现状态；新增控制器/视图/基础设施清单 |
| v1.2.0 | 2026-06-11 | 新增品牌/类型/轴承/商家完整 CRUD（新建/编辑/删除/恢复/彻底删除）；新增 Excel 导入在售轴承；新增 includeDeleted 复选框；权限管理从占位改为 db_admin RBAC 实现；DataController 从列表扩展到 20 个 Action |
| v1.3.0 | 2026-06-15 | Dashboard 从 3 个独立 search 端点改为统一 `/api/admin/dashboard/stats`；待审核数从 ❌ 改为 ✅ 已实现；新增 BearerTokenHandler 基础设施；移除已完成的待审核数统计 TODO |
