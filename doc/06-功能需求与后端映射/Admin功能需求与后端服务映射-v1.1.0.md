# Admin 功能需求与后端服务映射 v1.1.0

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
| 轴承总量 | API | `GET /api/bearings/search?page=1&pageSize=1` | ✅ 已实现 |
| 品牌总量 | API | `GET /api/brands?page=1&pageSize=1` | ✅ 已实现 |
| 商家总量 | API | `GET /api/merchants?page=1&pageSize=1` | ✅ 已实现 |
| 待审核数 | — | — | ❌ 需 API 端点 |

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
| 品牌列表 | API | `GET /api/brands` | ✅ 已实现 |
| 类型列表 | API | `GET /api/bearing-types` | ✅ 已实现 |
| 商家列表 | API | `GET /api/merchants` | ✅ 已实现 |
| 替代品弹窗 | API | `GET /api/proxy/interchanges/{id}` | ✅ 已实现 |
| 在售商品弹窗 | API | `GET /api/proxy/merchant-bearings/{id}` | ✅ 已实现 |

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
| 权限管理 | — | — | ❌ 占位页面 |
| 审计日志 | Identity | `GET /api/auditlog` | ✅ 已实现 |

### 第七层：选项设置

| 功能点 | 后端 | 端点 | Phase 1 状态 |
|--------|------|------|-------------|
| 配置列表 | API | `GET /api/config` | ✅ 已实现 |
| 编辑配置 | API | `PUT /api/config/{key}` | ✅ 已实现 |

## Phase 1 已实现清单

### 控制器（12 个）

| 控制器 | 职责 | 调用服务 |
|--------|------|---------|
| HomeController | 仪表盘 + /Home/Status | ServiceHealthService |
| DataController | 轴承/品牌/类型/商家列表 | API |
| CrawlerController | 爬虫列表 + 触发运行 | Crawler API |
| SyncController | ETL 控制面板 | Sync API |
| MappingController | 映射查看 | API |
| CorrectionController | 纠错审核 | API |
| LicenseController | 营业执照审核 | API |
| MerchantVerifyController | 商家认证 | API |
| UsersController | 用户管理 | Identity |
| PermissionController | 权限管理（占位） | — |
| AuditLogController | 审计日志 | Identity |
| ConfigController | 配置管理 | API |

### 视图（16 个）

| 视图 | 功能 |
|------|------|
| Home/Index | 仪表盘（4 统计卡片 + 服务状态表） |
| Data/Bearings | 轴承列表 + 搜索 + 分页 + 替代品弹窗 |
| Data/Brands | 品牌列表 + 搜索 + 分页 |
| Data/BearingTypes | 类型列表 + 搜索 + 分页 |
| Data/Merchants | 商家列表 + 搜索 + 分页 + 在售商品弹窗 |
| Crawler/Index | 爬虫列表 + 触发运行 |
| Sync/Index | ETL 控制面板 + 调度说明 |
| Mapping/Index | 品牌/类型映射查看 |
| Correction/Index | 纠错审核 + 状态筛选 |
| License/Index | 营业执照审核 + 通过/拒绝 |
| MerchantVerify/Index | 商家认证 + 搜索 |
| Users/Index | 用户管理 + 启用/禁用 |
| Permission/Index | 权限管理（占位） |
| AuditLog/Index | 审计日志列表 |
| Config/Index | 配置管理 + 编辑弹窗 |

### 基础设施

| 组件 | 说明 |
|------|------|
| Program.cs | 4 命名 HttpClient + ServiceHealthService + 2 代理端点 |
| ServiceHealthService | 并行检查 4 服务 /live 端点 |
| _Layout.cshtml | 固定侧边栏 + 顶栏 + 主题切换 + CDN 回退 |
| site.css | 亮色/暗色主题 CSS 变量 |
| site.js | 侧边栏折叠 + 主题切换 + 服务状态 AJAX + 弹窗交互 |

## Phase 2 待实现清单

| 功能 | 优先级 | 依赖 |
|------|--------|------|
| OpenIddict 认证登录 | 高 | Identity admin_client |
| 本地审计日志（db_admin） | 中 | PostgreSQL |
| ETL 任务历史列表 | 中 | Sync API 新端点 |
| 爬虫运行历史 | 中 | Admin DB 或 Crawler API |
| 品牌/类型映射管理 | 中 | Sync API 映射端点 |
| 待审核数统计 | 低 | API 端点 |

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始版本，完整的 Admin 功能与后端映射 |
| v1.1.0 | 2026-06-11 | 对齐 Phase 1 实际实现：标注已实现/待实现状态；新增控制器/视图/基础设施清单 |
