# OpenFindBearings.Admin

统一管理后台，提供品牌/类型/商家/轴承 CRUD、同步数据审核、审计日志、系统配置等功能。所有数据操作通过项目 API 代理，不直连业务数据库。

## 技术栈

- ASP.NET Core MVC（.NET 10.0）
- Bootstrap 5 + jQuery
- JWT Bearer 认证（通过 Identity OAuth）
- PostgreSQL（仅 admin 自有数据：审计日志、角色权限、系统配置）

## 核心功能

- **基础信息管理**：品牌、轴承类型、商家、轴承的 CRUD + 软删除恢复
- **审核中心**：同步数据审核、信息纠错审核、营业执照审核、入驻申请审批
- **任务管理**：ETL 各阶段运行状态监控、历史记录、手动触发
- **数据爬虫**：只读展示数据源和最近抓取状态
- **审计日志**：代理展示 API/Identity/Sync 三端审计日志
- **系统配置**：站点设置、价格显示、可信度阈值等运行时配置

## 构建与运行

```bash
cd OpenFindBearings.Admin
dotnet restore src/OpenFindBearings.Admin
dotnet run --project src/OpenFindBearings.Admin
```

默认端口 `https://localhost:7167`，登录需 Identity 服务。

## 部署

```bash
kubectl apply -f deploy/k3s/
```

- 域名：`admin.515813.xyz`
- 认证：通过 Identity OAuth（`auth.abcsxl.com`）
