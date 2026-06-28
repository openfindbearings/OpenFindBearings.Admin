# Admin 统一管理平台总体设计 v1.4.0

## 概述

OpenFindBearings.Admin 是一个大一统的超级管理后台，将 Crawler、Sync、API 数据、Identity 用户权限的运维管理整合到单一 ASP.NET Core MVC 项目中。Phase 1 已通过 BearerTokenHandler 实现 JWT 自动注入（从登录 cookie 提取认证 token 转发 API 请求）。Admin 拥有独立的 PostgreSQL 数据库（db_admin），用于存储 RBAC 角色权限映射和审计日志。

## 架构总览

```
浏览器
  │ HTTP/HTTPS
  ▼
OpenFindBearings.Admin（ASP.NET Core MVC）
  │ Phase 1 无认证（所有页面公开访问）
  │
  ├──→ Crawler API（:5078）           — 爬虫列表、状态、手动触发
  ├──→ Sync API（:5104）              — ETL 触发、状态、Excel 导入
  ├──→ OpenFindBearings.Api（:7183）  — 品牌/类型/轴承/商家 CRUD、纠错、配置
  ├──→ Identity API（:7201）          — 用户管理、角色、审计日志
  │
  └── db_admin（PostgreSQL）          — RBAC 角色权限、审计日志
```

## Phase 1 决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 认证 | BearerTokenHandler 自动注入 JWT（从 cookie 提取） | 后端 API 请求直接复用前端登录认证，无需重复登录 |
| 本地数据库 | db_admin（PostgreSQL） | RBAC 角色权限映射 + 审计日志 |
| SignalR | 不用 | 服务状态通过 AJAX 轮询，足够满足需求 |
| Chart.js | 不用 | 仪表盘使用简单卡片展示统计数据 |

## 数据访问原则

| 数据 | 方式 | 说明 |
|------|------|------|
| 品牌/类型/轴承/商家 | 调 API（OpenFindBearings.Api） | 业务数据，API 有权限+审计 |
| 纠错审核 | 调 API（OpenFindBearings.Api） | CorrectionRequest 端点 |
| ETL 触发/状态 | 调 Sync API | ETL 任务管理 |
| Excel 导入 | 调 Sync API（代理） | 前端通过 Admin 中转上传文件 |
| 爬虫启停 | 调 Crawler API | Crawler 自身管理运行状态 |
| 用户/角色/审计日志 | 调 Identity API | 认证数据，走 Identity 端点 |
| RBAC 角色权限 | db_admin 本地 | Admin 自行维护角色→权限映射 |
| 审计日志 | db_admin 本地 | Admin 操作审计记录 |

## 技术栈

| 层面 | 技术 | 说明 |
|------|------|------|
| 框架 | ASP.NET Core MVC (.NET 10) | 服务端渲染 Razor |
| ORM | EF Core + Npgsql | db_admin 数据库 |
| 认证 | Phase 1 无 | 后续接入 OpenIddict Code Flow |
| 前端 | Bootstrap 5 + jQuery + Font Awesome | libman 管理，CDN 回退 |
| 部署 | K3s Deployment (1 副本) | Dockerfile + kustomization |

## 项目结构

```
OpenFindBearings.Admin/
├── src/OpenFindBearings.Admin/
│   ├── Program.cs                     — 应用入口 + 健康检查 + 代理端点
│   ├── appsettings.json               — 端口、服务地址、连接字符串配置
│   ├── libman.json                    — Bootstrap + jQuery + Font Awesome
│   ├── Dockerfile
│   │
│   ├── Controllers/
│   │   ├── HomeController.cs          — 仪表盘 + /Home/Status 端点
│   │   ├── DataController.cs          — 轴承/品牌/类型/商家 CRUD（20 个 Action）
│   │   ├── CrawlerController.cs       — 爬虫列表 + 触发运行
│   │   ├── SyncController.cs          — ETL 控制面板
│   │   ├── MappingController.cs       — 品牌/类型映射查看
│   │   ├── CorrectionController.cs    — 信息纠错审核
│   │   ├── LicenseController.cs       — 营业执照审核
│   │   ├── MerchantVerifyController.cs — 商家认证审核
│   │   ├── UsersController.cs         — 用户管理（调 Identity）
│   │   ├── PermissionController.cs    — 权限管理（db_admin RBAC）
│   │   ├── AuditLogController.cs      — 审计日志（调 Identity）
│   │   └── ConfigController.cs        — 配置管理（调 API）
│   │
│   ├── Data/
│   │   ├── ApplicationDbContext.cs    — EF Core DbContext（4 个 DbSet）
│   │   ├── SeedData.cs                — 3 角色 + 17 权限种子数据
│   │   └── Migrations/                — EF Core 迁移
│   │
│   ├── Models/
│   │   ├── DTOs/                      — 11 个 DTO 类（独立文件）
│   │   │   ├── ApiPagedResponse.cs
│   │   │   ├── BearingItemDto.cs
│   │   │   ├── BrandItemDto.cs
│   │   │   ├── BearingTypeItemDto.cs
│   │   │   ├── MerchantItemDto.cs
│   │   │   ├── CorrectionItemDto.cs
│   │   │   ├── LicenseItemDto.cs
│   │   │   ├── CrawlerItemDto.cs
│   │   │   ├── SyncItemDto.cs
│   │   │   ├── UserItemDto.cs
│   │   │   ├── AuditLogItemDto.cs
│   │   │   ├── SystemConfigDto.cs
│   │   │   └── MappingItemDto.cs
│   │   ├── Entities/                  — 4 个实体类
│   │   │   ├── AdminAuditLog.cs
│   │   │   ├── AdminUserRole.cs
│   │   │   ├── AdminRolePermission.cs
│   │   │   └── AdminConfig.cs
│   │   ├── Enums/                     — 2 个枚举
│   │   │   ├── PermissionKey.cs       — 17 个权限键（Bearing* 命名）
│   │   │   └── AdminAction.cs         — 17 个审计动作
│   │   └── ViewModels/                — 5 个 ViewModel 类
│   │       ├── BearingListViewModel.cs
│   │       ├── BrandListViewModel.cs
│   │       ├── TypeListViewModel.cs
│   │       ├── MerchantListViewModel.cs
│   │       └── ErrorViewModel.cs
│   │
│   ├── Services/
│   │   ├── BearerTokenHandler.cs    — JWT 自动注入（cookie → Authorization header）
│   │   └── ServiceHealthService.cs  — 4 服务健康检查
│   │
│   ├── Views/
│   │   ├── Shared/_Layout.cshtml      — 固定侧边栏 + 顶栏 + 主题切换
│   │   ├── Home/Index.cshtml          — 仪表盘（统计卡片 + 服务状态表）
│   │   ├── Data/                      — 轴承/品牌/类型/商家列表页（含 CRUD）
│   │   ├── Crawler/Index.cshtml       — 爬虫列表
│   │   ├── Sync/Index.cshtml          — ETL 控制面板
│   │   ├── Mapping/Index.cshtml       — 映射查看
│   │   ├── Correction/Index.cshtml    — 纠错审核
│   │   ├── License/Index.cshtml       — 营业执照审核
│   │   ├── MerchantVerify/Index.cshtml — 商家认证
│   │   ├── Users/Index.cshtml         — 用户管理
│   │   ├── Permission/Index.cshtml    — 权限管理
│   │   ├── AuditLog/Index.cshtml      — 审计日志
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

## 页面导航（实际实现）

```
┌───────────────────────────────────────────────────┐
│ ≡  OpenFindBearings               🟢检测中  🌙    │
├───────────────────────────────────────────────────┤
│ 侧边栏                        │ 主内容区            │
│                                │                    │
│ 📊 仪表盘                     │ 统计卡片 + 服务状态 │
│                                │                    │
│ ▾ 信息管理                    │                    │
│   基础信息                     │                    │
│   ├ 品牌列表                   │ 新建+搜索+编辑+删除 │
│   ├ 类型列表                   │ 新建+搜索+编辑+删除 │
│   ├ 轴承列表                   │ 新建+搜索+替代品    │
│   └ 商家管理                   │ 新建+搜索+在售商品  │
│   审核管理                     │                    │
│   ├ 信息纠错审核               │ 状态筛选+审批/拒绝   │
│   ├ 营业执照审核               │ 通过/拒绝           │
│   └ 商家认证审核               │ 搜索+认证           │
│                                │                    │
│ 🔄 同步管理                   │ ETL 触发 + 调度说明 │
│ 🕷 爬虫管理                   │ 爬虫列表 + 触发运行  │
│                                │                    │
│ ▾ 认证管理                    │                    │
│   权限管理                     │                    │
│   ├ 用户管理                   │ 搜索 + 启用/禁用    │
│   ├ 权限管理                   │ db_admin RBAC       │
│   └ 审计日志                   │ 日志列表            │
│                                │                    │
│ ⚙ 系统配置                   │ 配置列表 + 编辑弹窗 │
│                                │                    │
├───────────────────────────────────────────────────┤
│         OpenFindBearings © 2026                   │
└───────────────────────────────────────────────────┘
```

## 数据库设计（db_admin）

Admin 拥有独立的 PostgreSQL 数据库，用于 RBAC 和审计：

| 表 | 用途 | 关键字段 |
|---|---|---|
| `admin_audit_logs` | Admin 操作审计 | Id, UserId, Username, Action, ResourceType, ResourceId, Detail, Result, IpAddress, CreatedAt |
| `admin_user_roles` | Admin 角色分配 | Id, UserId, RoleName, AssignedAt |
| `admin_role_permissions` | 角色权限映射 | Id, RoleName, PermissionKey, Granted, CreatedAt |
| `admin_configs` | Admin 本地配置 | Id, Key(unique), Value, Description, UpdatedAt |

### RBAC 架构

Identity 只负责 OIDC 认证和系统级 scope（admin/mobile/sync），Admin 自行维护角色-权限映射：

- **admin（超级管理员）**：全部 17 权限，含 DataRestore
- **业务员（editor）**：轴承/商家 CRUD + 纠错审核
- **审计员（viewer）**：仪表盘/审计日志/系统配置只读

### 权限键（PermissionKey）

使用 `Bearing*` 命名（非 `Product*`），共 17 个：

```
DashboardView, BearingView, BearingCreate, BearingEdit, BearingDelete,
MerchantView, MerchantManage, MerchantVerify, CorrectionReview,
EtlManage, CrawlerManage, RoleManage, UserManage,
SystemView, SystemManage, AuditView, DataRestore
```

## 主题系统

Admin 支持亮色/暗色主题切换，通过 CSS 变量实现：

| 变量 | 亮色 | 暗色 |
|------|------|------|
| `--bg-main` | `#f4f6f9` | `#1a1d21` |
| `--bg-card` | `#fff` | `#2b2f33` |
| `--text-primary` | `#212529` | `#e1e5eb` |
| `--border-color` | `#dee2e6` | `#3d4450` |

用户选择通过 `localStorage` 持久化，顶栏月亮/太阳图标切换。

## 优雅降级

Admin 启动时不依赖任何后端服务。ServiceHealthService 并行检查 4 个服务的 `/live` 端点：

- 首页顶部实时显示各服务在线/离线状态
- 所有 API 调用失败时返回空数据 + 错误提示
- 服务离线不影响 Admin 自身导航和已加载页面

## 待实现功能（Phase 2+）

| 功能 | 优先级 | 依赖 |
|------|--------|------|
| ETL 任务历史列表 | 中 | Sync API 新端点 |
| 爬虫运行历史 | 中 | Admin DB 或 Crawler API |
| 品牌/类型映射管理 | 中 | Sync API 映射端点 |

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始设计文档 |
| v1.1.0 | 2026-06-08 | 新增 db_admin 数据库设计；补充审计策略；更新数据访问原则 |
| v1.1.1 | 2026-06-09 | 更新导航结构图（新增可信度配置、Excel 导入、系统设置菜单）；优化侧边栏分类 |
| v1.2.0 | 2026-06-11 | 对齐 Phase 1 实际实现：移除 db_admin/SignalR/认证/Chart.js 等未实现内容；更新项目结构为实际文件布局；新增主题系统文档；新增待实现功能清单 |
| v1.3.0 | 2026-06-11 | 新增 db_admin 数据库（EF Core + Npgsql）；RBAC 角色权限映射（3 角色 17 权限）；DataController 20 个 CRUD Action；Excel 导入代理端点；品牌/类型/轴承/商家完整 CRUD + 软删除/恢复/彻底删除；权限键统一为 Bearing* 命名 |
| v1.4.0 | 2026-06-15 | 新增 BearerTokenHandler JWT 自动注入；Dashboard 改为 `/api/admin/dashboard/stats` 统一端点；API SeedData 补充 6 个 admin 权限（dashboard.view 等）；待审核数已实现；更新项目结构添加 BearerTokenHandler；认证从"跳过"改为"BearerTokenHandler" |
