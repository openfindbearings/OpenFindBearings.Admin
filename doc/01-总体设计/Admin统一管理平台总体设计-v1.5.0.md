# Admin 统一管理平台总体设计 v1.5.0

## 概述

OpenFindBearings.Admin 是一个大一统的超级管理后台，将 Crawler、Sync、API 数据、Identity 用户权限的运维管理整合到单一 ASP.NET Core MVC 项目中。认证使用 OAuth 授权码流程 + BearerTokenHandler JWT 注入。Admin 拥有独立的 PostgreSQL 数据库（db_admin），用于存储 RBAC 角色权限映射和审计日志。

## 架构总览

```
浏览器
  │ HTTP/HTTPS
  ▼
OpenFindBearings.Admin（ASP.NET Core MVC）
  │ OAuth 授权码流程登录（Identity /connect/authorize）
  │ BearerTokenHandler 从 cookie 提取 JWT 注入 ApiClient
  │
  ├──→ OpenFindBearings.Api（:7183）  — 品牌/类型/轴承/商家 CRUD、纠错、配置、仪表盘统计
  ├──→ FindBearings.Sync（:7206）     — ETL 触发、状态、同步审核、映射管理
  ├──→ FindBearings.Crawler（:7207）  — 爬虫列表、状态、手动触发
  ├──→ OpenFindBearings.Identity（:7201）— 用户管理、审计日志、系统配置
  │
  └── db_admin（PostgreSQL）          — RBAC 角色权限、审计日志
```

## 架构决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 认证 | OAuth 授权码流程 → BearerTokenHandler 注入 JWT | 后端 API 请求直接复用前端登录认证，无需重复登录 |
| 数据库 | db_admin（PostgreSQL） | RBAC 角色权限映射 + Admin 审计日志 |
| 部署 | K3s Deployment（1 副本） | Dockerfile + kustomization |
| CSS/JS | libman 管理，CDN 回退，集中存放于 wwwroot/lib/ | 不分散在各页面 |

## 数据访问原则

| 数据 | 方式 | 说明 |
|------|------|------|
| 品牌/类型/轴承/商家 | 调 API | 业务数据，API 有权限+审计 |
| 纠错审核 | 调 API | CorrectionRequest 端点 |
| 同步数据审核 | 调 Sync API | PendingReview 端点 |
| ETL 触发/状态 | 调 Sync API | ETL 任务管理 |
| 映射管理 | 调 Sync API | 品牌/类型映射 CRUD |
| 爬虫启停 | 调 Crawler API | Crawler 自身管理运行状态 |
| 用户/角色/审计日志/系统配置 | 调 Identity API | 认证和系统数据 |
| RBAC 角色权限 | db_admin 本地 | Admin 自行维护角色→权限映射 |
| Admin 审计日志 | db_admin 本地 | Admin 操作审计记录 |

## 项目结构

```
OpenFindBearings.Admin/
├── src/OpenFindBearings.Admin/
│   ├── Program.cs                     — 应用入口 + 健康检查 + 代理端点
│   ├── appsettings.json               — 端口、服务地址、连接字符串
│   ├── libman.json                    — Bootstrap + jQuery + Font Awesome
│   ├── Dockerfile
│   │
│   ├── Controllers/
│   │   ├── AccountController.cs       — 登录/回调/登出/修改密码
│   │   ├── HomeController.cs          — 仪表盘 + /Home/Status 端点
│   │   ├── DataController.cs          — 轴承/品牌/类型/商家 CRUD（20+ 个 Action）
│   │   ├── CrawlerController.cs       — 爬虫列表 + 触发运行
│   │   ├── SyncController.cs          — ETL 控制面板
│   │   ├── MappingController.cs       — 品牌/类型映射 CRUD（Sync API 代理）
│   │   ├── ReviewController.cs        — 同步数据审核（Sync /api/audit/*）
│   │   ├── CorrectionController.cs    — 信息纠错审核
│   │   ├── LicenseController.cs       — 营业执照审核
│   │   ├── MerchantVerifyController.cs — 商家认证审核
│   │   ├── UsersController.cs         — 用户管理（调 Identity）
│   │   ├── PermissionController.cs    — 权限管理（db_admin RBAC）
│   │   ├── AuditLogController.cs      — 审计日志（四服务源聚合）
│   │   └── ConfigController.cs        — 配置管理（调 API）
│   │
│   ├── Data/
│   │   ├── ApplicationDbContext.cs    — EF Core DbContext（4 个 DbSet）
│   │   ├── SeedData.cs                — 3 角色 + 17 权限种子数据
│   │   └── Migrations/                — EF Core 迁移
│   │
│   ├── Models/
│   │   ├── DTOs/                      — 13+ 个 DTO 类
│   │   ├── Entities/                  — 4 个实体类
│   │   ├── Enums/
│   │   │   ├── PermissionKey.cs       — 21 个权限键
│   │   │   └── AdminAction.cs         — 审计动作枚举
│   │   └── ViewModels/                — 5 个 ViewModel 类
│   │
│   ├── Services/
│   │   ├── BearerTokenHandler.cs      — JWT 自动注入（cookie → Authorization header）
│   │   └── ServiceHealthService.cs    — 4 服务健康检查（/health/live 端点）
│   │
│   ├── Views/
│   │   ├── Shared/_Layout.cshtml      — 固定侧边栏 + 顶栏 + 主题切换
│   │   ├── Home/Index.cshtml          — 仪表盘（4 统计卡片 + 待审核数 + 服务状态）
│   │   ├── Data/                      — 轴承/品牌/类型/商家列表（CRUD 弹窗）
│   │   ├── Crawler/Index.cshtml       — 爬虫列表
│   │   ├── Sync/Index.cshtml          — ETL 控制面板
│   │   ├── Mapping/Index.cshtml       — 品牌/类型映射管理
│   │   ├── Review/Index.cshtml        — 同步数据审核
│   │   ├── Correction/Index.cshtml    — 纠错审核
│   │   ├── License/Index.cshtml       — 营业执照审核
│   │   ├── MerchantVerify/Index.cshtml — 商家认证
│   │   ├── Users/Index.cshtml         — 用户管理
│   │   ├── Permission/Index.cshtml    — 权限管理
│   │   ├── AuditLog/Index.cshtml      — 审计日志（四服务 tab 切换）
│   │   └── Config/Index.cshtml        — 配置管理
│   │
│   ├── wwwroot/
│   │   ├── css/site.css               — 亮色/暗色主题 + 业务组件样式
│   │   ├── js/site.js                 — 侧边栏折叠 + 主题切换 + 服务状态 AJAX
│   │   └── lib/                       — Bootstrap 5 + jQuery + Font Awesome 6
│   │
│   └── deploy/k3s/                    — deployment + service + kustomization
│
└── doc/                               — 设计文档
```

## 侧边栏导航

```
≡ OpenFindBearings
├── 📊 仪表盘
├── ▾ 信息管理
│   ├── 基础信息
│   │   ├── 品牌列表
│   │   ├── 类型列表
│   │   ├── 轴承列表
│   │   └── 商家管理
│   ├── 映射信息
│   │   ├── 品牌映射
│   │   └── 类型映射
│   └── 审核管理
│       ├── 同步数据审核
│       ├── 信息纠错审核
│       ├── 营业执照审核
│       └── 商家认证审核
├── ▾ 任务管理
│   ├── ETL 同步
│   └── 数据爬虫
├── ▾ 认证管理
│   ├── 用户管理
│   ├── 权限管理
│   └── 审计日志
│       ├── 管理员
│       ├── API
│       ├── Sync
│       └── Crawler
└── ⚙ 系统配置
```

## 数据库设计（db_admin）

| 表 | 用途 | 关键字段 |
|---|---|---|
| admin_audit_logs | Admin 操作审计 | Id, UserId, Username, Action, ResourceType, ResourceId, Detail, Result, IpAddress, CreatedAt |
| admin_user_roles | Admin 角色分配 | Id, UserId, RoleName, AssignedAt |
| admin_role_permissions | 角色权限映射 | Id, RoleName, PermissionKey, Granted, CreatedAt |
| admin_configs | Admin 本地配置 | Id, Key(unique), Value, Description, UpdatedAt |

### RBAC 角色

- **admin（超级管理员）**：全部权限（含 DataRestore + DataHardDelete）
- **editor（业务员）**：轴承/商家 CRUD + 纠错审核
- **viewer（审计员）**：仪表盘/审计日志/系统配置只读

### 权限键（PermissionKey）

共 21 个权限键，使用 Bearing* 命名（非 Product*）：

```
DashboardView, BearingView, BearingCreate, BearingEdit, BearingDelete,
MerchantView, MerchantManage, MerchantVerify, CorrectionReview,
SyncView, SyncTrigger, CrawlerView, CrawlerTrigger,
EtlManage, CrawlerManage,
UserManage, RoleManage,
SystemView, SystemManage, AuditView,
DataRestore, DataHardDelete
```

## 健康检查

ServiceHealthService 并行检查 4 个服务的 `/health/live` 端点（统一路径），非阻塞初始化。

## 主题系统

亮色/暗色主题切换，CSS 变量方式实现，用户选择通过 `localStorage` 持久化。

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始设计文档 |
| v1.1.0 | 2026-06-08 | 新增 db_admin 数据库设计；补充审计策略；更新数据访问原则 |
| v1.1.1 | 2026-06-09 | 更新导航结构图；优化侧边栏分类 |
| v1.2.0 | 2026-06-11 | 对齐 Phase 1 实际实现：移除未实现内容；更新项目结构为实际文件布局；新增主题系统文档 |
| v1.3.0 | 2026-06-11 | db_admin 数据库（EF Core + Npgsql）；RBAC 3 角色 17 权限；DataController 20 个 CRUD Action；完整 CRUD + 软删除/恢复/彻底删除；权限键统一为 Bearing* |
| v1.4.0 | 2026-06-15 | BearerTokenHandler JWT 注入；Dashboard 统一端点；API SeedData 补 admin 权限 |
| v1.5.0 | 2026-07-06 | 侧边栏新增映射信息/任务管理/同步数据审核；ReviewController 审计日志四源聚合；健康检查路径改为 /health/live；PermisssionKey 补充至 21 个 |
