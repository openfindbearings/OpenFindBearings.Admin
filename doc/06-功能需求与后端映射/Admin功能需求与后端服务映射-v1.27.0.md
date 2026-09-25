# Admin 功能需求与后端服务映射 v1.27.0

## 概述

本文档整理 Admin 后台全部功能需求，逐一标注实际实现状态。✅ = 已实现，❌ = 待实现。

## 变更日志

### v1.21.1 → v1.22.0 更新内容

1. **弹窗体系统一（新增 `wwwroot/js/dialogs.js`，`_Layout` 全局挂载）**：全站 31 处原生 `confirm/alert/prompt`（散落 11 个视图）清零，统一为自绘覆盖层三件套——
   - `showConfirm(opts) → Promise<boolean>`：白卡确认框（危险操作红按钮），列表删除/恢复/硬删（`form[data-confirm]` + `data-confirm-danger` 属性委托拦截提交）、审批通过、认证、映射删除、审核拒绝等 18 处接入；
   - `showPrompt(opts) → Promise<string|null>`：带 textarea 的必填理由框，替换证照材料队列原生 `window.prompt`；
   - `showToast(type, msg)`：右上角 3 秒自动消失轻提示，替换全部裸 `alert`（含 `danger` 别名兼容 BS 命名）。
2. **自绘而非 BS modal 的原因**：BS modal 与 offcanvas 各有焦点陷阱，抽屉之上叠加时互相抢焦点导致 `data-bs-dismiss` 失灵；自绘层（z-index 3100/3200）在任意页面层级行为一致。表单类 BS modal（在售商家/在售商品/拒绝弹窗，均在普通页面使用不与抽屉叠加）维持现状。
3. **审批链全量审查修复**（同批，dotnet build 不检查内嵌 JS，本次起配套 node --check 语法门禁）：
   - dialogs.js `closeDialog` 的 `onOk()` 漏传参数——所有 showConfirm/showPrompt 点确认恒 resolve false（"点审核通过没反应"根因之一），改 `onOk(ok === true)`；
   - `btn-approve`/`btn-verify` 补 `e.stopPropagation()`——原漏加导致点按钮连带整行点击误开抽屉（"弹出侧边详情"即此现象）；
   - **抽屉底栏操作区**（`#drawerActions`）：Pending 显"审核通过/拒绝"、Active 未认证显"认证"，看完成员就地决策（原操作按钮只在行内、被 620px 抽屉盖住）；审批动作抽公共函数 `approveMerchant/verifyMerchant/openRejectDialog`，行按钮与底栏共用，成功后自动收抽屉刷新；
   - 从抽屉发起拒绝时先关抽屉再弹 rejectModal（BS modal 叠 offcanvas 会复现焦点陷阱，延时 320ms 等滑出动画结束）；
   - lightbox `openDocViewer` 的 `attr('src','')` 改 `removeAttr`（空串在部分浏览器触发当前页重新加载）；
   - 抽屉重开时加载占位复位"加载中..."（原失败文案残留）；
   - 证照材料队列"通过"补 `data-confirm` 确认（原直提易误点），与全站审批口径统一。
4. **申请认证队列信号（对齐 API v2.9.0）**：`MerchantItemDto` 加 `verifyRequested`；列表状态列 Active 未认证且已申请时显黄色"申请认证"徽标，抽屉状态行附"· 已申请认证"——商户主动申请的优先处理提示，Admin 点"认证"后 API 自动清除标记。

### v1.21.0 → v1.21.1 更新内容

1. **材料图片基址修复（真机验证暴露）**：新增配置 `Media:PublicBaseUrl`（默认 `https://bff.515813.xyz/media`，K8s env 可覆盖）。证照材料队列"查看原件"与入驻抽屉 `DOC_BASE`/`dLogo` 原拼 `ApiUrls:OpenFindBearingsApi`（K8s 内为集群内 svc 地址，浏览器不可达 → 图片 404 且 onerror 隐藏后卡片呈"不可点"），统一改公网媒体基址。
2. **抽屉材料卡重构**：缩略图主导（110px cover 裁切）、PDF 显文件图标、点缩略图弹 **lightbox** 大图；加载失败显示占位文案不再隐身。lightbox 为**自绘全屏遮罩**（非 BS modal——modal 与 offcanvas 各有焦点陷阱，抽屉上叠加时互相抢焦点导致 `data-bs-dismiss` 失灵关不掉）；点空白/✕/ESC 关闭，PDF 用 iframe 内嵌预览。
3. **抽屉状态/渠道映射大小写归一**：API 输出 status 为 PascalCase、applicationMode 为小写，原查表只匹配一种形态导致"状态 -"；归一后查表修复。**商家类型中文化**：列表与抽屉的类型列/行由英文枚举名改显中文（Manufacturer 生产厂家 / AuthorizedDealer 授权经销商 / Distributor 分销商 / Trader 贸易商）。
4. **队列数据完整性（对齐 API v1.5.1）**：商家列/提交人列正常显示（API `GetPendingAsync` 补 Include）；入驻审核中商户的随单材料不再混入变更队列（队列与仪表盘角标同口径排除），双入口重复审核问题消除。

### v1.20.0 → v1.21.0 更新内容

1. **证照材料审核（对齐 API《06 v2.7.0》4.7）**：`LicenseController`/`Views/License` 泛化改名为 `DocumentController`/`Views/Document`，页面从"营业执照审核"升级为"证照材料审核"——**定位收窄为入驻后的材料变更队列**（换证/补授权书/厂房照），随入驻申请提交的材料已改在入驻审批抽屉内级联审结。列表新增"材料类型"徽章列与原件"查看"链接（点开 API 主机直出文件）；**拒绝动作升级为必填理由**（表单 prompt → POST body `reason`，商户端可见；旧实现拒绝无原因）。
2. **通过不再自动认证**：`Approve` 动作与文案同步 API 语义解耦——认证改由入驻申请审批页"认证"按钮触发，API 按"必备材料全部已通过"矩阵口径校验（缺项 400 文案透传给审核人）。
3. **入驻审批抽屉新增"申请证照材料"区**：`MerchantVerifyController.Detail` 服务端合并 `GET /api/admin/merchants/{id}/documents` 进 `data.documents`，抽屉渲染类型/状态徽章 + 缩略图 + 驳回意见（材料请求失败降级不阻断审批）。
4. **菜单/看板/审计映射更名**：侧边栏与 dashboard 卡片"营业执照审核"→"证照材料审核"（统计字段 `pendingLicenses`→`pendingDocuments`，元素 id 同步）；`AuditLogItemDto` 动作映射补 `Approve/RejectDocument`（旧 `Approve/RejectLicense` 保留兼容历史行）、区域映射 `documents`=材料。

### v1.19.0 → v1.20.0 更新内容

1. **入驻申请审批页主流化重构（MerchantVerify）**：对齐主流平台"先看全资料再决策"。① **详情抽屉**：点击行右侧滑出 offcanvas（`Detail` 动作代理 `GET /api/admin/merchants/{id}`），展示企业名称/信用代码/渠道/提交时间/联系方式/简介/Logo/拒绝原因全量资料，企业名称缺失标黄"建议驳回补全"；**爬虫历史数据比对表内嵌抽屉**（复用 `GetMerchantBearings`，含"显示全部"切换），行内独立"同步数据"按钮移除。② **拒绝弹窗结构化**：预设原因分类下拉（资料不完整/营业执照信息不符/疑似重复入驻/涉嫌违规经营/其他）+ 补充说明必填，提交拼接"分类：说明"（随站内信与驳回原因回传申请人）。③ **列表升级**：新增"渠道、提交时间"两列（数据源 API `MerchantDto` 补 `applicationMode/submittedAt/rejectReason`，`MerchantItemDto` 同步）。④ **并发冲突处理**：approve/reject 收 409（`MERCHANT_ALREADY_PROCESSED`）时提示"该申请已被处理"并自动刷新；控制器透传 API ProblemDetails `detail` 文案。⑤ **根因修复**：API `ToPublicDto` 漏映射 `Status` 导致列表状态恒空、"审核通过/拒绝"按钮条件永不成立（此前按钮不显示的真正原因，非镜像版本问题）。⑥ **Bootstrap 5.3 API 修正**：原 `$('#rejectModal').modal('show')` 为 BS4 jQuery 插件写法在 BS5 下静默失效，统一改 `bootstrap.Modal/Offcanvas.getOrCreateInstance`。

2. **审核菜单与仪表盘卡片顺序调整**：侧边栏"审核管理"子菜单与 dashboard 第二行卡片中，**入驻申请审批前置于营业执照审核**（入驻生效是主链路、认证是后置增值）；状态徽标渲染补 Draft 分支兜底（未知状态显示原文而非误标"待审核"）。

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
- v1.27.0 积分任务管理页（/Points，system.view 权限组）：赚分规则表格批量保存（分值/每日上限/连续阶梯/启停开关），代理 API /api/admin/points/rules，改完实时生效不发版；复选框隐藏 false 垫底保证停用可提交；合规红线（不可充值/提现/转让）页头标注。
- v1.25.0 商户管理页新增"解除归属"操作（POST /api/admin/merchants/{id}/detach，权限键 merchant.detach 迁移补插存量库）：认领商户清场回公海（商品/证照/成员/邀请/纠错全清，随爬取自然更新、可再认领），自建商户被 API 拒绝并透传原因；data-confirm-danger 二次确认；权限分组与中文名两处映射同步。
- v1.25.0 后台权限体系接线 API RBAC（三员分立轻量落地）：① 登录门禁——OIDC 回调后调 API GET /api/me/permissions，无面板角色（Admin/Operator/Auditor）即拒登，权限清单写 cookie 多值 claim；② Cookie 事件 30 分钟复核（SecurityStamp 模式），权限收回自动踢出；③ 菜单按 permission claim 动态渲染（Admin 角色全显示）；④ 13 个 Controller 挂 [PanelPermission] 动态策略（PanelPolicyProvider 按需生成）；⑤ 角色/权限管理页改代理 API /api/admin/roles*（权限点目录只读），本地 db_admin 僵尸表（admin_user_roles/admin_role_permissions）与种子整体删除；⑥ 用户页新增"平台角色"弹窗（by-auth 端点差量分配，自锁守卫禁移自己 Admin）。 |
- v1.25.0 解除归属按钮可用性动态化（与 API DetachMerchant 守卫严格同口径）：仅 status=Active 且（applicationMode=Claim 或 None+dataSource=Manual 提名已有）可点；其余置灰+title 说明（自建用删除/公海无归属可解）。API MerchantDto/映射补 dataSource 字段（v1.31.0）。
