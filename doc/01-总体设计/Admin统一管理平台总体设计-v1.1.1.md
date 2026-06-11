# Admin 统一管理平台总体设计 v1.0.0

## 概述

OpenFindBearings.Admin 是一个大一统的超级管理后台，将 Crawler、Sync、API 数据、Identity 用户权限的运维管理整合到单一 ASP.NET Core MVC 项目中。Admin 拥有自己的小型 PostgreSQL 数据库（db_admin），仅存储 Admin 专属数据（审计日志、映射规则、运行历史），业务数据全部通过调用各服务的 HTTP API 操作。

## 架构总览

```
浏览器
  │ HTTPS
  ▼
OpenFindBearings.Admin（ASP.NET Core MVC）
  │ OpenIddict 认证（Code Flow + PKCE）
  │
  ├──→ Crawler API（:5078）           — 启停、手动触发
  ├──→ Sync API（:5104）              — ETL 触发、状态、审核配置
  ├──→ OpenFindBearings.Api（:7183）  — 品牌/类型/轴承/商家 CRUD
  ├──→ Identity API（:7201）          — 登录、用户、角色、审计日志
  │
  └──→ db_admin（PostgreSQL，Admin 专属）
       ├── AdminAuditLogs    — 审计日志（谁在 Admin 做了什么）
       ├── BrandMappings     — 品牌名称→Code 映射
       ├── TypeMappings      — 类型名称→Code 映射
       ├── CrawlerRunHistory — 爬虫运行历史（每次触发的记录）
       └── AdminConfigs      — Admin 自身配置
```

## 数据访问原则

| 数据 | 方式 | 理由 |
|------|------|------|
| 品牌/类型/轴承/商家 | 调 API（OpenFindBearings.Api） | 业务数据，API 有权限+审计 |
| ETL 触发/状态/审核 | 调 API（Sync API） | 操作 Sync 内部状态，不能直连 DB |
| 爬虫启停 | 调 API（Crawler API） | Crawler 自身管理运行状态 |
| 用户/角色/登录 | 调 API（Identity API） | 认证数据，走 OpenIddict 协议 |
| 审计日志（Admin 操作） | 写 Admin DB | 其他项目不记录这些操作 |
| 品牌/类型映射规则 | 读/写 Admin DB | Mapping 由 Admin 管理，Sync 通过配置读取 |
| 爬虫运行历史 | 写 Admin DB | Crawler 无审计功能，Admin 自行记录 |

## 技术栈

| 层面 | 技术 | 说明 |
|------|------|------|
| 框架 | ASP.NET Core MVC (.NET 10) | 服务端渲染 Razor |
| 认证 | OpenIddict Code Flow + PKCE | Admin 作为 confidential client |
| 实时通信 | SignalR | 服务状态推送、ETL 进度 |
| 前端 | Bootstrap 5 + jQuery + Chart.js | 管理后台标准栈 |
| 部署 | K3s Deployment (1 副本) | 与现有项目一致 |
| 可观测 | OpenTelemetry + Serilog | 日志/指标/链路追踪 |

## 项目结构

```
OpenFindBearings.Admin/
├── OpenFindBearings.Admin.slnx
├── Program.cs
├── appsettings.json
│
├── Controllers/
│   ├── HomeController.cs              — 首页仪表盘
│   ├── CrawlerController.cs           — 爬虫管理
│   ├── SyncController.cs              — 同步管理
│   ├── DataController.cs              — 数据查询/编辑
│   ├── MappingController.cs           — 映射维护
│   ├── ImageController.cs             — 图片修正
│   ├── ConfigController.cs            — 配置管理
│   └── AccountController.cs           — 登录/退出
│
├── Data/
│   ├── AdminDbContext.cs              — EF Core DbContext（db_admin）
│   ├── AdminDbContextFactory.cs       — 设计时迁移工厂
│   ├── SeedData.cs                    — 种子数据
│   └── Entities/
│       ├── AdminAuditLog.cs           — 审计日志实体
│       ├── BrandMapping.cs            — 品牌名称→Code 映射
│       ├── TypeMapping.cs             — 类型名称→Code 映射
│       ├── CrawlerRunRecord.cs        — 爬虫运行记录
│       └── AdminConfig.cs            — Admin 配置项
│
├── Services/
│   ├── CrawlerApiClient.cs            — 调 Crawler API
│   ├── SyncApiClient.cs               — 调 Sync API
│   ├── BusinessApiClient.cs           — 调 OpenFindBearings.Api
│   ├── IdentityApiClient.cs           — 调 Identity API
│   ├── ServiceHealthService.cs        — 定时检查各服务健康状态
│   └── AuditService.cs               — Admin 本地审计日志记录
│
├── ViewModels/
│   ├── DashboardViewModel.cs
│   ├── Crawler/
│   ├── Sync/
│   ├── Data/
│   ├── Mapping/
│   ├── Image/
│   └── Account/
│
├── Views/
│   ├── Home/
│   ├── Crawler/
│   ├── Sync/
│   ├── Data/
│   ├── Mapping/
│   ├── Image/
│   ├── Account/
│   └── Shared/
│       ├── _Layout.cshtml
│       └── _ServiceStatus.cshtml
│
├── Hubs/
│   └── StatusHub.cs                   — SignalR Hub 推送状态变化
│
├── Auth/
│   └── OpenIddictExtensions.cs        — OpenIddict 客户端配置
│
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── lib/
│
├── deploy/
│   ├── k3s/
│   │   ├── deployment.yml
│   │   ├── kustomization.yaml
│   │   ├── secret-template.yml
│   │   └── DEPLOY.md
│   └── .github/workflows/
│       └── build.yml
│
└── doc/
    └── ...
```

## 页面导航

```
┌───────────────────────────────────────────────────┐
│ OpenFindBearings Admin                    [用户] [退出] │
│ 🟢 Crawler | 🟢 Sync | 🟢 API | 🟢 Identity    │ ← 服务状态条
├───────────────────────────────────────────────────┤
│ 侧边栏                        │ 主内容区            │
│                                │                    │
│ 📊 仪表盘                     │                    │
│ 🌐 爬虫管理                   │                    │
│   ├ 爬虫列表/状态             │                    │
│   ├ 运行历史                  │                    │
│   ├ 数据源配置                │                    │
│   └ 手动触发                  │                    │
│ 🔄 同步管理 (ETL)             │                    │
│   ├ ETL 状态                  │                    │
│   ├ 手动触发                  │                    │
│   ├ Excel 导入                │                    │
│   ├ 重试失败                  │                    │
│   ├ 数据源可信度配置          │                    │
│   └ 同步日志                  │                    │
│ 📦 数据管理                   │                    │
│   ├ 品牌管理                  │                    │
│   ├ 类型管理                  │                    │
│   ├ 轴承管理                  │                    │
│   ├ 商家管理                  │                    │
│   └ 替代品管理                │                    │
│ 🏷 映射维护                   │                    │
│   ├ 品牌名称→Code             │                    │
│   └ 类型名称→Code             │                    │
│ 🖼 图片管理                   │                    │
│ 📋 配置管理                   │                    │
│ 👥 用户权限                   │                    │
│   ├ 用户管理                  │                    │
│   └ 角色管理                  │                    │
│ ⚙️ 系统设置                   │                    │
│   ├ 业务参数配置              │                    │
│   └ 价格策略                  │                    │
│                                │                    │
└───────────────────────────────────────────────────┘
```

## 认证流程

```
1. 用户访问 Admin 任意页面
2. 未登录 → 重定向到 Account/Login
3. Admin 构造 OpenIddict 授权请求（跳转到 Identity）：
4. 用户跳转到 Identity 登录页输入凭证
5. Identity 返回 authorization code
6. Admin 用 code + client_secret 换取 access_token + refresh_token
7. Admin 将 token 存入加密 Cookie
8. 后续请求携带 token 调用各服务 API
9. Token 过期 → 自动用 refresh_token 刷新
```

认证委托给 OpenFindBearings.Identity 处理，Admin 不存任何用户凭证。

## Admin 数据库设计（db_admin）

### 技术选型

| 项 | 选择 |
|----|------|
| 数据库 | PostgreSQL（与现有项目一致） |
| 数据库名 | `db_admin` |
| ORM | EF Core + Npgsql |
| 迁移 | Code-First 迁移 |

### 表结构

#### AdminAuditLogs — 审计日志

Admin 所有敏感操作（触发爬虫/ETL、编辑映射、修改配置、登录/退出）都记录到此表。

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid PK | |
| UserId | Guid? | 操作人（Admin 自身用户系统） |
| Username | string | 操作人用户名 |
| Action | string | 操作类型（TriggerCrawler / TriggerETL / UpdateMapping / Login / Logout 等） |
| ResourceType | string? | 资源类型（Crawler / Sync / Mapping / Config / Auth） |
| ResourceId | string? | 资源标识（如爬虫名、映射 ID） |
| Detail | string? | 操作详情（JSON 格式） |
| Result | string | Success / Failure / Partial |
| IpAddress | string? | 客户端 IP |
| CreatedAt | DateTime | |

#### BrandMappings — 品牌名称→Code 映射

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid PK | |
| RawName | string | 原始名称（如"天马"、"TIANMA BEARING"） |
| StandardCode | string | 标准 Code（如"TMB"） |
| Source | string | 来源（AutoMatch / ManualConfirm / ManualFix） |
| Confidence | int | 可信度（1-100） |
| CreatedBy | Guid? | 创建人 |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime? | |

#### TypeMappings — 类型名称→Code 映射

结构同 BrandMappings，`RawName` → `StandardCode`（如"圆柱滚子轴承" → "CRB"）。

#### CrawlerRunRecords — 爬虫运行历史

Crawler 自身无审计功能，每次 Admin 触发爬虫时，Admin 自行记录。

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid PK | |
| CrawlerName | string | 爬虫名称（如 cbia_crawler） |
| TriggeredBy | string | 触发人（admin 或 cronjob） |
| TriggeredAt | DateTime | 触发时间 |
| Status | string | Pending / Running / Success / Failed |
| CompletedAt | DateTime? | |
| ItemsCount | int | 处理数量 |
| ErrorMessage | string? | |
| Duration | TimeSpan? | 运行时长 |

#### AdminConfigs — Admin 自身配置

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid PK | |
| Key | string（唯一） | 配置键 |
| Value | string | 配置值（JSON 格式） |
| Description | string? | |
| UpdatedAt | DateTime? | |

### 为什么不直接 SQLite

1. K3s Pod 是临时性的，SQLite 文件随 Pod 删除而丢失
2. 多副本部署时 SQLite 存在写冲突
3. 现有基础设施已经是 PostgreSQL，加一个 `db_admin` 数据库零额外运维成本

## 优雅降级

Admin 启动时不依赖任何后端服务。ServiceHealthService 定时轮询 4 个服务的 `/live` 端点：

- 首页顶部固定显示各服务在线状态
- 在线服务 → 正常展示操作按钮
- 离线服务 → 对应页面显示"服务不可用"提示，操作按钮禁用
- 离线期间 Admin 自身导航、已缓存的数据浏览不受影响

## 审计策略

| 操作类型 | 记录到哪里 | 说明 |
|---------|-----------|------|
| 登录/退出 | Identity AuditLog + Admin AuditLog | Identity 记录标准登录事件，Admin 记录界面操作上下文 |
| 用户/角色管理（通过 Admin） | Identity AuditLog | 通过 Identity API 操作，由 Identity 自动记录 |
| 品牌/类型/轴承 CRUD（通过 Admin） | API AuditLog | 通过 API 操作，由 API 项目自动记录 |
| 触发爬虫（通过 Admin） | Admin AuditLog + CrawlerRunRecords | Crawler 无审计，Admin 自行记录 |
| 触发 ETL（通过 Admin） | Sync ETL 任务历史 + Admin AuditLog | Sync 有任务记录，Admin 加一笔界面操作日志 |
| 修改映射规则 | Admin AuditLog | Admin 自己的数据，自己记录 |
| 修改 Admin 配置 | Admin AuditLog | Admin 自己的数据，自己记录 |

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始设计文档 |
| v1.1.0 | 2026-06-08 | 新增 db_admin 数据库设计；补充审计策略；更新数据访问原则 |
| v1.1.1 | 2026-06-09 | 更新导航结构图（新增可信度配置、Excel 导入、系统设置菜单）；优化侧边栏分类 |
