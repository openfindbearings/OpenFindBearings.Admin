# Admin 功能需求与后端服务映射 v1.0.0

## 概述

本文档整理 Admin 后台全部功能需求，逐一标注：
- 对应哪个后端服务
- 后端现有端点是否已满足
- 是否需要新增端点
- Admin 侧是调用 API 还是直读自己的 DB

## 功能总览

### 第一层：仪表盘（首页 Dashboard）

| 功能点 | 数据源 | 现有端点 | 状态 | Admin 方案 |
|--------|--------|---------|------|-----------|
| Crawler 在线状态 | Crawler API | `/live` ✅ | 可用 | ServiceHealthService 定时轮询 |
| Sync 在线状态 | Sync API | `/health/live` ✅ | 可用 | 同上 |
| API 在线状态 | API | `/live` ✅ | 可用 | 同上 |
| Identity 在线状态 | Identity API | `/live` ✅ | 可用 | 同上 |
| 轴承总量 | API | `GET /api/admin/dashboard/stats` ✅ | 可用 | 直接调用 API |
| 商家总量 | API | 同 stats 端点 ✅ | 可用 | 同 |
| 同步统计 | Sync API | `GET /api/monitor/metrics` ✅ | 可用 | 直接调用 Sync API |
| 爬虫待处理 Detail 数 | Crawler API | ❌ 无端点 | **需新增** | Crawler API 新增 status 端点 |
| 待审核更正数 | API | `GET /api/admin/corrections/pending` ✅ | 可用 | 直接调用 API |
| 待审核商家数 | API | `GET /api/admin/merchants?verifiedOnly=false` ✅ | 可用 | 直接调用 API |
| 今日审计日志数 | Identity API | `GET /api/auditlog/today-count` ✅ | 可用 | 调用 Identity API |

### 第二层：爬虫管理

| 功能点 | 后端 | 现有端点 | 状态 | Admin 方案 |
|--------|------|---------|------|-----------|
| 爬虫列表 | Crawler API | ❌ 无 | **需新增** | `GET /api/crawlers` |
| 爬虫详情/状态 | Crawler API | ❌ 无 | **需新增** | `GET /api/crawlers/{name}/status` |
| 启动爬虫 | Crawler API | ❌ 无 | **需新增** | `POST /api/crawlers/{name}/run` |
| 配置查看 | Crawler API | ❌ 无 | **需新增** | `GET /api/crawlers/{name}/config` |
| 运行历史 | Crawler API | ❌ 无 | **需新增** | `GET /api/crawlers/{name}/history` |
| 爬虫统计（轴承/商家数） | Crawler API | ❌ 无 | **需新增** | 同 status 端点带回 |
| 快速跳过判断 | — | 代码已实现 | — | Admin 读取 status 即可 |

**Crawler 需要新增的端点（在 Program.cs 添加 Minimal API）：**

```csharp
GET    /api/crawlers                    → 列表
GET    /api/crawlers/{name}/status      → 状态（含运行中/空闲、上次运行时间、待处理数）
POST   /api/crawlers/{name}/run         → 触发运行（后台 Task.Run，返回 202 + jobId）
GET    /api/crawlers/{name}/history     → 运行历史（从内存或 DB 查）
```

### 第三层：ETL 同步管理

| 功能点 | 后端 | 现有端点 | 状态 | Admin 方案 |
|--------|------|---------|------|-----------|
| 触发全量 ETL | Sync API | `POST /api/etl/run` ✅ | 可用 | 调用 Sync API |
| 触发 E 阶段 | Sync API | `POST /api/etl/extract` ✅ | 可用 | 同 |
| 触发 T 阶段 | Sync API | `POST /api/etl/transform` ✅ | 可用 | 同 |
| 触发 L 阶段 | Sync API | `POST /api/etl/load` ✅ | 可用 | 同 |
| ETL 状态 | Sync API | `GET /api/etl/tasks/{id}` ✅ | 可用 | 同 |
| ETL 历史列表 | Sync API | ❌ 无列表端点 | **需新增** | `GET /api/etl/tasks` 分页+筛选 |
| ETL 重试 | Sync API | ❌ 无重试端点 | **需新增** | `POST /api/etl/tasks/{id}/retry` |
| 审核待处理列表 | Sync API | `GET /api/audit/pending` ✅ | 可用 | 调用 Sync API |
| 审核通过/拒绝 | Sync API | `POST /api/audit/{id}/approve` ✅ | 可用 | 同 |
| 指标总览 | Sync API | `GET /api/monitor/metrics` ✅ | 可用 | 同 |
| 图片重试 | Sync API | `POST /api/images/retry/batch` ✅ | 可用 | 同 |

**Sync 建议新增的端点：**

```csharp
GET    /api/etl/tasks          → 任务历史列表（分页+按阶段/状态/日期筛选）
POST   /api/etl/tasks/{id}/retry → 重试失败任务
GET    /api/etl/summary        → 暂存数据概览（各实体按状态统计）
```

### 第四层：业务数据管理（品牌/类型/轴承/商家）

| 功能点 | 后端 | 现有端点 | 状态 | Admin 方案 |
|--------|------|---------|------|-----------|
| 品牌列表 | API | `GET /api/brands` ✅ | 可用 | 调用 API（公开） |
| 品牌 CRUD（管理员） | API | `POST/PUT /api/admin/brands` ✅ | 可用 | 调用 API（需 Admin 角色） |
| 类型列表 | API | `GET /api/bearing-types` ✅ | 可用 | 调用 API（公开） |
| 类型 CRUD（管理员） | API | `POST/PUT /api/admin/bearing-types` ✅ | 可用 | 调用 API |
| 轴承列表（公开） | API | `GET /api/bearings/search` ✅ | 可用 | 调用 API |
| 轴承 CRUD（管理员） | API | `CRUD /api/admin/bearings` ✅ | 可用 | 调用 API |
| 商家列表（管理员） | API | `GET /api/admin/merchants` ✅ | 可用 | 调用 API |
| 商家 CRUD（管理员） | API | `CRUD /api/admin/merchants` ✅ | 可用 | 调用 API |
| 商家认证（管理员） | API | `POST /api/admin/merchants/{id}/verify` ✅ | 可用 | 调用 API |
| 更正待审核 | API | `GET /api/admin/corrections/pending` ✅ | 可用 | 调用 API |
| 更正通过/拒绝 | API | `POST /api/admin/corrections/{id}/approve` ✅ | 可用 | 调用 API |
| 替代品列表 | API | ❌ 公开接口没有替代品列表 | **可用** | Admin 可通过业务逻辑获取 |
| 商家-轴承关联 | API | `GET /api/merchants/{id}/bearings` ✅ | 可用 | 调用 API |

### 第五层：映射维护（品牌名→Code、类型名→Code）

这里是**设计分歧点**，需要你决策：

**方案 A：映射数据存在 Admin 自己的 DB，由 Admin 管理**
```
Admin DB: BrandMappings(原始名称, 标准Code, 来源, 可信度)
          TypeMappings(原始名称, 标准Code, 来源, 可信度)
Admin 提供 CRUD 页面，API 无感知
Sync 需要时... 需要从 Admin 读取 → 要么 Admin 暴露 API，要么 Sync 直接读 Admin 的 DB
```

**方案 B：映射数据存在 API 项目（通过批量同步接口）**
```
Admin → 调用 API 的 POST /api/sync/brands/batch
      → 调 API 的 POST /api/sync/bearingtypes/batch
      → 品牌/类型本身已经是标准数据，映射在 T 阶段做
      → Admin 只需要管理品牌/类型的标准 Code 列表
```

**方案 C：映射数据存在 Sync 项目**
```
Sync 已有品牌映射/类型映射的 CRUD 端点（/api/config/brands, /api/config/types）
Admin → 直接调 Sync API 维护映射
Sync 的 T 阶段直接使用自己管理的映射
```

**我的推荐：方案 C**

Sync 已经有完整的品牌映射和类型映射 CRUD：

| 端点 | 存在 | 说明 |
|------|------|------|
| `GET /api/config/brands/` | ✅ | 分页列表 |
| `POST /api/config/brands/` | ✅ | 创建 |
| `PUT /api/config/brands/{id}` | ✅ | 更新 |
| `DELETE /api/config/brands/{id}` | ✅ | 删除 |
| `GET /api/config/types/` | ✅ | 类型映射列表 |
| `POST /api/config/types/` | ✅ | 创建 |
| `PUT /api/config/types/{id}` | ✅ | 更新 |
| `DELETE /api/config/types/{id}` | ✅ | 删除 |

为什么推荐 C：
1. Sync 的 T 阶段已经基于这些映射做标准化
2. 数据离使用者最近，没有跨服务同步延迟
3. Admin 只需要调 Sync API 即可完成管理
4. 不需要 Admin 自己建映射表

### 第六层：图片管理

| 功能点 | 后端 | 现有端点 | 状态 | Admin 方案 |
|--------|------|---------|------|-----------|
| 失败图片列表 | Sync API | `GET /api/images/failed` ✅ | 可用 | 调用 Sync API |
| 单张重试 | Sync API | `POST /api/images/retry/{mappingId}` ✅ | 可用 | 同 |
| 批量重试 | Sync API | `POST /api/images/retry/batch` ✅ | 可用 | 同 |
| 上传替换 | Sync API | `POST /api/images/upload` ✅ | 可用 | 同 |
| 浏览轴承图片 | API | `GET /api/bearings/{id}` ✅ | 可用 | 调用 API |
| 编辑轴承图片 URL | API | `PUT /api/admin/bearings/{id}` ✅ | 可用 | 调用 API |
| 图片健康检查批量扫描 | 建议 Admin 自己实现 | ❌ | **建议新增** | Admin 循环调 API 获取轴承列表 + HEAD 检查 URL |

### 第七层：配置管理

| 功能点 | 后端 | 现有端点 | 状态 | Admin 方案 |
|--------|------|---------|------|-----------|
| Identity 系统配置 | Identity API | `GET/PUT /api/systemconfig` ✅ | 可用 | 调用 Identity API |
| API 系统配置 | API | `GET/PUT /api/admin/config` ✅ | 可用 | 调用 API（需 Admin 角色） |
| Sync 告警规则 | Sync API | `CRUD /api/config/alerts/rules` ✅ | 可用 | 调用 Sync API |
| Sync CronJob 调度 | K3s 层面 | — | — | Admin 只读展示，修改需 kubectl |
| Crawler 配置 | Crawler API | ❌ | **需新增** | `GET/PUT /api/crawlers/{name}/config` |

### 第八层：用户权限

| 功能点 | 后端 | 现有端点 | 状态 | Admin 方案 |
|--------|------|---------|------|-----------|
| 用户列表 | Identity API | `GET /api/account/admin/users` ✅ | 可用 | 调用 Identity API |
| 用户详情 | Identity API | `GET /api/account/admin/users/{id}` ✅ | 可用 | 同 |
| 创建用户 | Identity API | `POST /api/account/admin/users` ✅ | 可用 | 同 |
| 更新用户 | Identity API | `PUT /api/account/admin/users/{id}` ✅ | 可用 | 同 |
| 删除用户 | Identity API | `DELETE /api/account/admin/users/{id}` ✅ | 可用 | 同 |
| 启用/禁用 | Identity API | `PATCH /api/account/admin/users/{id}/status` ✅ | 可用 | 同 |
| 解锁用户 | Identity API | `POST /api/account/admin/users/{id}/unlock` ✅ | 可用 | 同 |
| 重置密码 | Identity API | `POST /api/account/admin/users/{id}/reset-password` ✅ | 可用 | 同 |
| 角色列表 | Identity API | `GET /api/role/` ✅ | 可用 | 同 |
| 创建角色 | Identity API | `POST /api/role/` ✅ | 可用 | 同 |
| 删除角色 | Identity API | `DELETE /api/role/{id}` ✅ | 可用 | 同 |
| 客户端列表 | Identity API | `GET /api/application/` ✅ | 可用 | 同 |
| 客户端 CRUD | Identity API | `CRUD /api/application/` ✅ | 可用 | 同 |
| 审计日志 | Identity API | `GET /api/auditlog/` ✅ | 可用 | 同 |

**Identity 从 Admin 角度完全覆盖，不需要新增任何端点。**

### 第九层：Admin 自身功能

| 功能点 | 存哪 | 说明 |
|--------|------|------|
| Admin 操作审计 | **Admin DB** → `AdminAuditLogs` | 记录谁什么时间在 Admin 做了什么 |
| 品牌映射数据 | **推荐用 Sync API** | 直接调 Sync 的映射端点 |
| 类型映射数据 | **推荐用 Sync API** | 直接调 Sync 的映射端点 |
| 图片健康扫描结果 | 临时，不持久化 | 下次扫描覆盖 |
| 用户偏好 | **Admin DB** → `UserPreferences` | UI 布局、表格列显隐等 |
| ServiceHealth 状态 | 内存，不持久化 | 定时刷新 |

## 总结：各项目需要新增的端点

### Crawler 项目（需要你授权修改）

```
需要新增的端点：

GET    /api/crawlers                          ← 爬虫列表
GET    /api/crawlers/{name}/status            ← 爬虫状态详情
POST   /api/crawlers/{name}/run               ← 手动触发
GET    /api/crawlers/{name}/history           ← 运行历史
GET    /api/crawlers/{name}/config            ← 查看配置
```

这些端点不需要 Quartz，不需要 Controller，直接在 `Program.cs` 里用 Minimal API 加几组 `MapGet`/`MapPost` 即可。

### Sync 项目（建议新增）

```
GET    /api/etl/tasks                         ← 任务历史列表
POST   /api/etl/tasks/{id}/retry              ← 重试失败任务
```

### API 项目（无需新增）

Admin 需要的所有业务端点已存在。唯一可能有用的是增加用户列表端点（当前 API 没有 `GET /api/admin/users`，但 Admin 的用户管理走 Identity API 而非 API 项目，所以不需要）。

### Identity 项目（无需新增，你说了不算）

所有用户/角色/客户端/审计日志端点已完整。Admin 直接调 Identity 的 `admin/users` 和 `role` 端点即可。

---

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-06-08 | 初始版本，完整的 Admin 功能与后端映射 |
