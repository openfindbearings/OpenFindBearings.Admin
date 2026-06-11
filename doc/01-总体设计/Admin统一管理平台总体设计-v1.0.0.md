# Admin 统一管理平台总体设计 v1.0.0

## 概述

OpenFindBearings.Admin 是一个大一统的超级管理后台，将 Crawler、Sync、API 数据、Identity 用户权限的运维管理整合到单一 ASP.NET Core MVC 项目中。Admin 自身不建数据库，不直接连接任何数据库，所有业务数据和操作需求都通过调用各服务的 HTTP API 实现。

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
  └──→ Identity API（:7201）          — 登录、用户、角色、审计日志
```


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
├── Services/
│   ├── CrawlerApiClient.cs            — 调 Crawler API
│   ├── SyncApiClient.cs               — 调 Sync API
│   ├── BusinessApiClient.cs           — 调 OpenFindBearings.Api
│   ├── IdentityApiClient.cs           — 调 Identity API
│   └── ServiceHealthService.cs        — 定时检查各服务健康状态
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
│   ├ 重试失败                  │                    │
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

Admin 在授权请求中申请的 Scope：

| Scope | 说明 |
|-------|------|
| `openid` | OpenID Connect 标准 |
| `profile` | 用户资料 |
| `roles` | 角色 |
| `offline_access` | 获取 refresh_token |

## 优雅降级

Admin 启动时不依赖任何后端服务。ServiceHealthService 定时轮询 4 个服务的 `/live` 端点：

- 首页顶部固定显示各服务在线状态
- 在线服务 → 正常展示操作按钮
- 离线服务 → 对应页面显示"服务不可用"提示，操作按钮禁用
- 离线期间 Admin 自身导航、已缓存的数据浏览不受影响

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始设计文档 |
