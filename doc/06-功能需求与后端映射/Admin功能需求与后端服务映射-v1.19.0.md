# Admin 功能需求与后端服务映射 v1.19.0

## 概述

本文档整理 Admin 后台全部功能需求，逐一标注实际实现状态。✅ = 已实现，❌ = 待实现。

## 变更日志

### v1.18.0 → v1.19.0 更新内容

1. **Profile 页 Token 刷新修复**：修复 `AccountController.Profile()` 中 `TryRefreshTokenAsync` 的返回值语义——原实现返回 `bool` 后从 `HttpContext.User` 重读 token，但同一请求内 cookie 更新不会反映到 `HttpContext.User`，导致重试仍用旧 token。改为返回 `string?`（新 access_token），Profile 直接用返回值调用 Identity API，确保刷新后使用新 token。

2. **Profile 页时间戳本地化**：修复 Razor 在 `<script>` 标签内 HTML 编码 `+` 为 `&#x2B;` 导致 JS 正则无法识别时区偏移的问题。改用 `data-raw` 属性传递原始时间戳字符串，JS 从 `getAttribute("data-raw")` 读取后转换为本地时间显示。

3. **基础信息列表头部统一**：品牌列表/类型列表/轴承列表/用户管理四个页面的 card-header 统一结构——外层 `d-flex flex-wrap`，内层 `d-flex flex-wrap gap-2`，"新建"按钮文字 `d-none d-sm-inline`（小屏仅图标），checkbox 移入 form 内并加 `d-none d-sm-block`（小屏隐藏）。修复类型/轴承列表 checkbox 在 form 外的老版布局。

4. **爬虫数据源表真实数据**：数据源表"最近抓取"和"状态"列从硬编码 `-`/`--` 改为从 `/api/proxy/etl/summary` 的 `crawler.lastCrawlAt` 和 `crawler.hasStuckProcessing` 读取真实值。加载顺序调整为先加载 summary（获取 lastCrawlAt），再加载数据源列表渲染。

5. **系统配置死项清理**：确认 `EnableRegistration`/`RequireEmailVerification`/`DefaultUserRole`（User 组）和 `Price.DefaultVisibility`（Price 组，预留但无消费方）均为无代码读取的死配置。最终保留 13 项：站点设置 4 项、价格显示 3 项（预留）、数据同步 6 项。

### v1.17.0 → v1.18.0 更新内容

1. **系统配置分组精简**：原 4 组（站点设置 / 用户设置 / 价格显示 / 数据同步）精简为 3 组。删除无消费方的"用户设置"组（`EnableRegistration` / `RequireEmailVerification` / `DefaultUserRole`）与遗留的 `ItemsPerPage`、`Cache.*` 配置键——这些键自创建起无任何代码读取，属死配置。保留 13 个有真实消费方的配置项：站点设置 4 项、价格显示 4 项、数据同步 6 项。

2. **价格配置项消费（Admin 端）**：新增 `PriceConfigService`（Singleton + 5 分钟进程内缓存），经 API `GET /api/admin/config/price` 读取 4 项价格配置。
   - `Price.ShowNegotiableLabel`：在售商品弹窗价格列，当商品价格描述含"电议/面议"且该开关开启时附加"[议价]"角标；关闭时仅显示原始价格文本。
   - `Price.NumericForSorting`：`DataController.GetMerchantBearings` 在 `sortBy=price` 时先读配置，关闭则直接返回"价格排序功能已关闭"；前端价格列头同时置为不可点击并给出提示。
   - `ConfigController.Update` 保存配置后调用 `PriceConfigService.Invalidate()` 主动失效缓存，无需等待 TTL 过期。

3. **在售商品弹窗增加价格列**：原 6 列（型号 / 曾用代号 / 类型 / 尺寸 / 品牌 / 状态）扩展为 7 列，新增"价格"列。价格单元格按 `IsPriceVisible` 决定显示实际价格文本还是"登录查看"，按 `IsNegotiable && ShowNegotiableLabel` 决定是否附加"[议价]"角标。

4. **价格配置消费（API 端配套）**：API 侧新增 `PriceParser` 领域服务与 `IPriceConfigProvider`（Singleton + 5 分钟缓存），使 `Price.ExtractPattern` 与 `Price.DefaultVisibility` 在商品创建与批量导入时真正生效——此前 `MerchantBearing.NumericPrice` 与 `PriceVisibility` 从未被赋值，属死字段。详见《价格体系设计 v0.2.0》。

### v1.14.0 → v1.15.0 更新内容

1. **轴承列表在售商家反向查询**：API 新增 `GET /api/bearings/{id:guid}/merchants`（按轴承反查销售该轴承的商家，支持 onlyOnSale 与分页）；Admin 新增代理端点 `GET /api/proxy/bearing-merchants/{id}` 及轴承列表页"在售商家"按钮与弹窗；点击商家行跳转 `/Data/Merchants?merchantId=...&search=...` 并自动打开该商家"在售商品"弹窗（复用现有弹窗），无需新建独立商家详情页。

2. **基础信息列表头部响应式适配**：品牌/类型/轴承列表的 card-header 与搜索表单改为 `flex-wrap`，手机屏不再溢出；"新建"按钮文本在小屏（<sm）隐藏，仅保留加号图标。

### v1.15.0 → v1.16.0 更新内容

1. **任务管理页增强（运行态 + 最近执行）**：任务管理页（Sync/Index）改造为展示四张状态卡片（Extract/Transform/Load/爬虫最近执行），各阶段卡片显示"进行中/排队中"或"空闲"及最近状态；新增"最近执行记录"表格（阶段/状态/开始/结束/成功/失败/跳过/错误信息），支持按阶段筛选与手动刷新；页面每 15 秒经代理端点轮询刷新。手动触发按钮在对应阶段存在活跃任务（Pending 或 Running，含定时调度 CronJob 创建的任务）时自动禁用，并提示"该阶段当前有进行中或排队中的任务（可能来自定时调度），暂不能手动执行"。

2. **新增 ETL 代理与后端端点**：Admin 新增代理端点 `GET /api/proxy/etl/summary`、`GET /api/proxy/etl/tasks`；Sync API 新增 `GET /api/etl/summary`（各阶段运行态 + 最近任务 + 爬虫最近执行情况聚合，爬虫数据由 CbiaDbContext 聚合 db_raw 的 cbia_* 表 sync_status 分布与最新 crawled_at，不新增爬虫落库表）、`GET /api/etl/tasks`（ETL 任务历史分页，支持 CommandType/Status 过滤）。手动触发端点（/run、/extract、/transform、/load）新增冲突守卫：对应阶段存在 Pending 或 Running 任务时返回 409，避免与每日 04:00-07:00 UTC 定时调度冲突。

### v1.16.0 → v1.17.0 更新内容

1. **系统配置新增可信度阈值与站点展示项**：后端 `SystemConfigs` 新增 `Reliability.AutoSyncThreshold` / `Reliability.ReviewThreshold` / `Reliability.DefaultSourceScore`（可信度三阈值，供 Sync 运行时拉取，替代其 appsettings 默认值）与 `Site.BeiAn` / `Site.CustomerService`（前端/移动端展示）。Admin 系统配置页（GET/PUT /api/admin/config）已可直接查看与编辑这些键，无需新增页面。

### v1.13.0 → v1.14.0 更新内容

1. **Dashboard 商家统计三分类**：首行商家总数卡内嵌"已入驻 X" 格式（如 0/690），第二行第 4 卡由"认证商家数量"改为"入驻申请待审批"，不统计爬虫商家数量。卡片文字变更：h3 显示 0/690（已入驻/总数），标题改"商家数量（已入驻/总数）"、移除原内嵌小字。
