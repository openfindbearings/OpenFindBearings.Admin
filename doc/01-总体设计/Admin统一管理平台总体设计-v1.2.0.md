# Admin 统一管理平台总体设计 v1.2.0

## 概述

OpenFindBearings.Admin 是一个大一统的超级管理后台，将 Crawler、Sync、API 数据、Identity 用户权限的运维管理整合到单一 ASP.NET Core MVC 项目中。Phase 1 阶段 Admin 不直连数据库，不启用认证，所有业务数据通过调用各服务的 HTTP API 操作。认证、审计日志、映射管理等功能留待后续阶段实现。

## 架构总览

```
浏览器
  │ HTTP/HTTPS
  ▼
OpenFindBearings.Admin（ASP.NET Core MVC）
  │ Phase 1 无认证（所有页面公开访问）
  │
  ├──→ Crawler API（:5078）           — 爬虫列表、状态、手动触发
  ├──→ Sync API（:5104）              — ETL 触发、状态
  ├──→ OpenFindBearings.Api（:7183）  — 品牌/类型/轴承/商家 CRUD、纠错、配置
  ├──→ Identity API（:7201）          — 用户管理、角色、审计日志
  │
  └── 无本地数据库（Phase 1）
```

## Phase 1 决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 认证 | 跳过，所有页面公开 | 先验证功能，最后统一加认证层 |
| 本地数据库 | 不建 db_admin | 审计日志/映射管理等暂不需要 |
| SignalR | 不用 | 服务状态通过 AJAX 轮询，足够满足需求 |
| Chart.js | 不用 | 仪表盘使用简单卡片展示统计数据 |

## 数据访问原则

| 数据 | 方式 | 说明 |
|------|------|------|
| 品牌/类型/轴承/商家 | 调 API（OpenFindBearings.Api） | 业务数据，API 有权限+审计 |
| 纠错审核 | 调 API（OpenFindBearings.Api） | CorrectionRequest 端点 |
| ETL 触发/状态 | 调 Sync API | ETL 任务管理 |
| 爬虫启停 | 调 Crawler API | Crawler 自身管理运行状态 |
| 用户/角色/审计日志 | 调 Identity API | 认证数据，走 Identity 端点 |

## 技术栈

| 层面 | 技术 | 说明 |
|------|------|------|
| 框架 | ASP.NET Core MVC (.NET 10) | 服务端渲染 Razor |
| 认证 | Phase 1 无 | 后续接入 OpenIddict Code Flow |
| 前端 | Bootstrap 5 + jQuery + Font Awesome | libman 管理，CDN 回退 |
| 部署 | K3s Deployment (1 副本) | Dockerfile + kustomization |

## 项目结构

```
OpenFindBearings.Admin/
├── src/OpenFindBearings.Admin/
│   ├── Program.cs                     — 应用入口 + 健康检查 + 代理端点
│   ├── appsettings.json               — 端口、服务地址配置
│   ├── libman.json                    — Bootstrap + jQuery + Font Awesome
│   ├── Dockerfile
│   │
│   ├── Controllers/
│   │   ├── HomeController.cs          — 仪表盘 + /Home/Status 端点
│   │   ├── DataController.cs          — 轴承/品牌/类型/商家列表
│   │   ├── CrawlerController.cs       — 爬虫列表 + 触发运行
│   │   ├── SyncController.cs          — ETL 控制面板
│   │   ├── MappingController.cs       — 品牌/类型映射查看
│   │   ├── CorrectionController.cs    — 信息纠错审核
│   │   ├── LicenseController.cs       — 营业执照审核
│   │   ├── MerchantVerifyController.cs — 商家认证审核
│   │   ├── UsersController.cs         — 用户管理（调 Identity）
│   │   ├── PermissionController.cs    — 权限管理（占位）
│   │   ├── AuditLogController.cs      — 审计日志（调 Identity）
│   │   └── ConfigController.cs        — 配置管理（调 API）
│   │
│   ├── Models/
│   │   ├── DTOs/                      — 13 个 DTO 类（独立文件）
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
│   │   ├── ViewModels/                — 5 个 ViewModel 类
│   │   │   ├── BearingListViewModel.cs
│   │   │   ├── BrandListViewModel.cs
│   │   │   ├── TypeListViewModel.cs
│   │   │   ├── MerchantListViewModel.cs
│   │   │   └── ErrorViewModel.cs
│   │   └── ErrorViewModel.cs
│   │
│   ├── Services/
│   │   └── ServiceHealthService.cs    — 4 服务健康检查
│   │
│   ├── Views/
│   │   ├── Shared/_Layout.cshtml      — 固定侧边栏 + 顶栏 + 主题切换
│   │   ├── Home/Index.cshtml          — 仪表盘（统计卡片 + 服务状态表）
│   │   ├── Data/                      — 轴承/品牌/类型/商家列表页
│   │   ├── Crawler/Index.cshtml       — 爬虫列表
│   │   ├── Sync/Index.cshtml          — ETL 控制面板
│   │   ├── Mapping/Index.cshtml       — 映射查看
│   │   ├── Correction/Index.cshtml    — 纠错审核
│   │   ├── License/Index.cshtml       — 营业执照审核
│   │   ├── MerchantVerify/Index.cshtml — 商家认证
│   │   ├── Users/Index.cshtml         — 用户管理
│   │   ├── Permission/Index.cshtml    — 权限管理（占位）
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
│   ├ 轴承列表                   │ 搜索+分页+替代品弹窗│
│   ├ 品牌管理                   │ 搜索+分页           │
│   ├ 类型管理                   │ 搜索+分页           │
│   └ 商家管理                   │ 搜索+分页+在售商品   │
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
│   ├ 权限管理                   │ 占位               │
│   └ 审计日志                   │ 日志列表            │
│                                │                    │
│ ⚙ 选项设置                   │ 配置列表 + 编辑弹窗 │
│                                │                    │
├───────────────────────────────────────────────────┤
│         OpenFindBearings © 2026                   │
└───────────────────────────────────────────────────┘
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
| OpenIddict 认证（登录/登出） | 高 | Identity admin_client |
| 本地审计日志（db_admin） | 中 | PostgreSQL |
| SignalR 实时状态推送 | 低 | 可选 |
| ETL 任务历史列表 | 中 | Sync API 新端点 |
| 爬虫运行历史 | 中 | Admin DB 或 Crawler API |
| 品牌/类型映射管理 | 中 | Sync API 映射端点 |
| 图片管理独立页面 | 低 | 可嵌入轴承/商家列表 |

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始设计文档 |
| v1.1.0 | 2026-06-08 | 新增 db_admin 数据库设计；补充审计策略；更新数据访问原则 |
| v1.1.1 | 2026-06-09 | 更新导航结构图（新增可信度配置、Excel 导入、系统设置菜单）；优化侧边栏分类 |
| v1.2.0 | 2026-06-11 | 对齐 Phase 1 实际实现：移除 db_admin/SignalR/认证/Chart.js 等未实现内容；更新项目结构为实际文件布局；新增主题系统文档；新增待实现功能清单 |
