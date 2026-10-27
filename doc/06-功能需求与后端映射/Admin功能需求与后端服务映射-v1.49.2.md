# Admin 功能需求与后端服务映射 v1.49.2

## 概述

本文档整理 Admin 后台全部功能需求，逐一标注实际实现状态。✅ = 已实现，❌ = 待实现。

## 变更日志

### v1.49.1 → v1.49.2（2026-10-27 轴承主图维护）

1. **轴承编辑弹窗加图片维护**（`Views/Data/Bearings.cshtml` + `Controllers/DataController.cs`）：3D/2D 文件选择、当前图预览、「清空」勾选，表单 `enctype=multipart/form-data`。
2. **接口映射**：文本字段仍走 `PUT /api/admin/bearings/{id:guid}`；图片按需走 `POST /api/admin/bearings/{id:guid}/images`（API v2.21.3 新增，multipart，`bearing.edit`）。文本成功、图片失败时文本保留并提示图片失败原因。
3. **权限**：`EditBearing` 显式声明 `bearing.edit`，与新建（`bearing.create`）口径一致。
4. **不做**：图片纠错审核入口不开放；商家实拍图不在本模块，后续由商家商品资料维护。

### v1.49.0 → v1.49.1（2026-10-25 去建档可用性修复）

1. **新建轴承弹窗补必填项**（`Views/Data/Bearings.cshtml` + `DataController.CreateBearing`）：品牌下拉、类型下拉与内径/外径/宽度，修复"线索去建档保存必然校验失败"。
2. **线索页权限收紧**：`BearingLeads` 动作显式要求 `bearing.view`，入口按同一权限显隐。
3. **失败原因透传**：新建、线索忽略/恢复与列表加载失败时显示上游 `detail`/`title`/`errors` 可读原因。

### v1.48.1 → v1.49.0（2026-10-25 型号线索待建档）

1. **新增型号线索页**（`Controllers/DataController.cs` + `Views/Data/BearingLeads.cshtml`）：`BearingLeads` 列表按 `status=pending|ignored|resolved`、`keyword`、分页读取 API `/api/admin/bearing-leads`；`IgnoreLead` / `RestoreLead` 两个写动作。
2. **轴承列表加入口与预填**（`Views/Data/Bearings.cshtml`）：顶部「型号线索」按钮；带 `newPartNumber` / `newBrand` 查询参数进入时自动打开新建弹窗并预填，用于线索页「去建档」一跳到位。
3. **背景**：移动端发布寻货 / 应答报价的型号是自由文本，平台型号库未收录时既不能定向匹配也无法上架，此前这些型号静默丢失；API v2.21.0 起把"库里根本没有的型号"记为线索，管理侧需要出口决定是否建档。
4. **权限**：列表沿用 `bearing.view`，忽略/恢复需 `bearing.edit`，建档保存沿用 `bearing.create`；不新增权限键。

### v1.48.0 → v1.48.1（2026-10-20 牌级在售状态口径修正）

1. **在售状态按实时件数判定**（`Views/MerchantGrade/Index.cshtml` + `Models/DTOs/MerchantItemDto.cs`）：列表「在售状态」改为读取 API 新下发的 `onSaleCount`/`onSaleMin`，显示 `在售达标 N/M` 或 `在售不足 N/M`；此前用"当前牌级 &gt; 普通"反推，牌级未及时重算时会误报达标。
2. **背景**：API v1.58.1 在 `GET /api/admin/merchants` 的 `MerchantDto` 增 `onSaleCount`/`onSaleMin`，管理页据此展示真实在售状态。

### v1.47.2 → v1.48.0（2026-10-20 商家卡级改牌级 + 有效期制）

1. **牌级页改名与字段扩展**（`Controllers/MerchantGradeController.cs` + `Views/MerchantGrade/Index.cshtml`）：
   「商家卡级」改为「商家牌级」，侧栏与页面标题同步；列表新增「有效期至」（`gradePeriodEndAt`，普通档显示横线）与「在售状态」（保级中显示 `保级至 yyyy-MM-dd`、已认证达标显示 `达标`、未认证显示 `未认证`）。
2. **DTO 补字段**（`Models/DTOs/MerchantItemDto.cs`）：新增 `GradePeriodStartAt`/`GradePeriodEndAt`/`GradeGraceUntil`（对齐 API `MerchantDto`）。
3. **调级弹窗文案**：档位改 1 普通 / 2 铜牌 / 3 银牌 / 4 金牌；未认证提示改为"仅可设为普通"；调级成功后按新档位重发一期。
4. **纠错联动文案**（`Views/Correction/Index.cshtml`）：卡级申诉相关提示统一改"牌级"，行为不变（采纳不自动改级，仍需二次确认）。
5. **背景**：API v1.58.0 将牌级改为 180 天有效期制（升级/续期重发一期、到期按本期成交续期或降档、在售连续不足直接降普通），管理侧必须能看到"哪一期、何时到期、当前是否在保级"，否则无法解释商家掉级与处理申诉。

### v1.47.1 → v1.47.2（2026-10-20 寻货术语统一）

1. **文案改词**（`Views/Sourcing/Index.cshtml`）：治理列表「发布方式」列与详情弹窗由 `邀标 N 家` / `公开招标` 改为 `邀请 N 家` / `公开`，并去除"招投标"类比措辞。
2. **无契约变更**：`isPrivate`、`invitedCount`、`invitedMerchantIds` 字段与代理路径不变。
3. **背景**：本平台做信息撮合，不设招投标流程，术语需与移动端「公开 / 邀请」保持一致。

### v1.47.0 → v1.47.1（2026-10-20 卡级申诉联动 + 联系方式查看留痕）

1. **纠错审核页「去调级」直达**（`Models/DTOs/CorrectionItemDto.cs` 补 `TargetId`；`Views/Correction/Index.cshtml`）：
   目标为 `Merchant` 且字段为 `merchantGrade` 的待审纠错单，操作列新增「去调级」——跳转 `MerchantGrade/Index` 并携带 `merchantId`/`merchantName`/`reason`/`grade`（建议值按"金/银/铜/注册"折算 4/3/2/1）。
   采纳该类纠错**不会自动改级**（避免纠错审核变成绕开治理的改级后门），确认文案已明确说明。
2. **商家卡级页申诉预填**（`Controllers/MerchantGradeController.cs` + `Views/MerchantGrade/Index.cshtml`）：
   `Index` 接受申诉上下文参数，页面顶部显示「来自商家卡级申诉」提示（商家、申诉理由），进入即预填并弹出调级弹窗（认证态未知时不禁用档位，交由 API 400 拦截）。
   调级成功后跳转干净路径，避免申诉参数导致弹窗反复弹出。
3. **需求方手机号查看留痕**：Admin 仅详情抽屉显示完整号码（列表页不下发号码，无需脱敏处理）；
   API `GET /api/admin/sourcing/demands/{id}` 每次下发手机号时写审计 `Action=ViewPublisherContact`、`EntityType=SourcingDemand`，
   用于"运营可取证"与"批量导出风险"的分离（审计失败不影响详情返回）。
4. **回执未计分原因**（`Views/Sourcing/Feedbacks.cshtml`）：随 API v1.57.1 的 `myFeedback.note`，复核页「未计分原因 / 复核留痕」列直接展示原因文本。

### v1.46.0 → v1.47.0（2026-10-19 成交回执复核 + 商家卡级人工调级）

1. **新增「成交回执」页**（`Controllers/SourcingController.cs` 增 `Feedbacks`/`RevokeFeedback` + `Views/Sourcing/Feedbacks.cshtml`）：代理 API `GET /api/admin/sourcing/feedbacks`（权限 `sourcing.view`）与 `POST /api/admin/sourcing/feedbacks/{responseId}/revoke`（权限 `merchant.grade.manage`）；
   展示轴承型号 / 需求方 / 商家 / 洽谈结果 / 是否计分 / 未计分原因或复核留痕 / 回执时间，可按商户与结果筛选，支持「撤销计分 / 恢复计分」（需填原因，写审计）。
2. **新增「商家卡级」页**（`Controllers/MerchantGradeController.cs` + `Views/MerchantGrade/Index.cshtml`）：代理既有 `GET /api/admin/merchants` 列表与新增 `POST /api/admin/merchants/{id}/grade`（权限 `merchant.grade.manage`）；
   列表认证与卡级分列，操作列「调整卡级」弹窗填目标卡级 + 原因；**卡级仅可授予已认证商户**（API 侧拦截，未认证授予返回 400），调整写审计日志。
3. **权限键**：新增 `merchant.grade.manage`（商家管理维度），由 API 迁移 `AddGradeManagePermission` 补插并授予 Admin/Operator；`merchant.grade.manage` 已加入 `PermissionController` 分组与中文名。
4. **商家列表 DTO**（`Models/DTOs/MerchantItemDto.cs`）：新增 `GradeDisplay`（对齐 API `MerchantDto.gradeDisplay`），列表直接展示卡级中文名，前端不再维护枚举到中文的映射。
5. **侧边栏**（`Views/Shared/_Layout.cshtml`）：寻货管理组加「成交回执」（`sourcing.view`）、商家管理组加「商家卡级」（`merchant.grade.manage`）。
6. **背景**：API v1.57.0 把"选定"升级为"确认合作 + 成交回执"，且商家卡级改由"近 180 天计分成功撮合"驱动；管理侧必须能复核/撤销误计分与人工干预异常卡级，故补此两页。

### v1.45.0 → v1.46.0（2026-10-19 寻货治理列表标出「已删除」单）

1. **行 DTO 补字段**（`Controllers/SourcingController.cs` 的 `SourcingAdminItemDto`）：新增 `IsDeleted`（对齐 API `GET /api/admin/sourcing/demands` 新输出的 `isDeleted`）。
2. **列表标出已删除单**（`Views/Sourcing/Index.cshtml`）：型号列在单号后按 `IsDeleted` 渲染 `已删除` 徽标（`badge bg-secondary`）。
3. **详情弹窗标出已删除单**：标题行状态徽标后追加 `已删除` 徽标（详情接口同步输出 `isDeleted`）。
4. **背景**：API v1.56.0 起寻货治理列表**显式包含软删单**（原硬排除 `IsDeleted`，发布人撤销的终态单从管理员视野消失，无从审计）。既然单子重新出现，就必须能一眼分辨"在库单 / 已被发布人删除的单"，故两处均加标记。

### v1.44.0 → v1.45.0（2026-10-19 寻货应答报表明细按行渲染 + 发布方身份）

1. **应答明细改按行渲染**（`Views/Sourcing/Index.cshtml` 详情弹窗）：应答自 API v1.5.0 起为多行标书（型号/报价/库存/交期挂在 `items[]` 行上），原实现读顶层 `x.price`/`x.stock`/`x.leadTime`（早已不存在）导致**所有应答恒显示"仅留言"**、治理视图看不到任何报价；改为遍历 `x.items` 逐行渲染型号与可选报价/库存/交期。
2. **发布人列补发布方身份**（列表 + 详情）：新增 `SourcingAdminItemDto.PublisherType`/`PublisherMerchantName`（对齐 API `GET /api/admin/sourcing/demands`、详情新增的 `publisherMerchantId`/`publisherMerchantName`/`publisherType`）——商户名义发布的单在发布人列下方标注商户名，详情行显示"商户名义 / 个人名义"，便于治理判断责任主体。

### v1.43.0 → v1.44.0（2026-10-19 寻货治理视图透出「发布方式」：公开 / 邀请）

1. **列表加列**（`Views/Sourcing/Index.cshtml`）：状态列之后新增「发布方式」列——`isPrivate` 为真显示 `邀请 N 家` 徽标（`badge bg-info text-dark`），否则「公开」；空态行 `colspan` 7→8。
2. **详情弹窗加行**：新增「发布方式」一行，邀请显示"邀请（不进大厅，仅通知 N 家认证商家）"、公开显示"公开（进入寻货大厅，人人可应答）"。
3. **行 DTO 补字段**（`Controllers/SourcingController.cs` 的 `SourcingAdminItemDto`）：新增 `IsPrivate`/`InvitedCount`（对齐 API `GET /api/admin/sourcing/demands` 输出；详情弹窗直取 API 返回的 `isPrivate`/`invitedMerchantIds`）。
4. **背景**：寻货自 API v1.54.0 起区分公开与邀请（受众与曝光范围不同），治理侧需一眼可辨"这单触达了谁"，否则无法判断投诉/下架影响面。

### v1.42.0 → v1.43.0（2026-10-18 替代关系独立权限键：替代品管理由 merchant.verify 改为 interchange.view/manage）

1. **权限独立**：替代关系（替代品）已是平台主数据，权限从借用 `merchant.verify`（认证审核语义）改为独立键 `interchange.view`（查看替代关系）与 `interchange.manage`（维护替代关系）。权限目录 32→34 键，迁移 `AddInterchangePermissions`。
2. **替代品管理页判权**：`InterchangesController` 类特性由 `[PanelPermission("merchant.verify")]` 改为 `[PanelPermission("interchange.view")]`；页面进入需 `interchange.view`。
3. **写按钮判权**：`Views/Interchanges/Index.cshtml` 内新建/编辑/删除按钮由 `PanelPerms.Has(User, "interchange.manage")` 控制（原借用 `merchant.verify`）。
4. **菜单判权**：`Views/Shared/_Layout.cshtml`「替代品管理」入口显隐由 `interchange.view` 控制（原 `merchant.verify`）；`PermissionController` 新增 `interchange` → 「数据管理」分组映射与两键显示名回退。
5. **端点判权**：`GET /api/admin/interchanges` 需 `interchange.view`；`POST/PUT/DELETE /api/admin/interchanges[/{id:guid}]` 需 `interchange.manage`。

### v1.41.0 → v1.42.0（2026-10-18 替代品字典化：删除"替代品审核"页与代理，管理页去状态筛选）

1. **删除「替代品审核」页**（`InterchangeReviewController` + `Views/InterchangeReview/`）与侧边栏入口：替代关系回归平台维护的主数据（字典），不再有"商家报候选 → 平台审核生效"队列。
2. **替代品管理页去审核态**（`Views/Interchanges/Index.cshtml`）：删除"审核状态"筛选下拉与"状态/提出商户"两列（含驳回理由展示），表格列 9→7（管理员含操作列 8）；页头说明改为"平台维护…；商家/用户发现替代关系有误可通过纠错提交、由平台核实后维护"。
3. **代理收敛**（`Program.cs`）：删除 `/api/proxy/admin-interchanges` 的 **GET 列表**、**GET /pending**、**POST /{id}/approve**、**POST /{id}/reject** 四条（保留 新建/编辑/删除 三条；列表页由 `InterchangesController` 服务端直取 API）。
4. **`InterchangesController`**：`Index` 去掉 `reviewStatus` 参数与 `ViewBag.ReviewStatus`；行 DTO 删 `reviewStatus`/`reviewStatusLabel`/`reviewComment`/`proposedByMerchantId`/`proposedByMerchantName`。
5. **降级 JSON**：`HomeController` 的 `interchanges` 占位对象删 `pendingCount`（对齐 API 仪表盘字段裁剪）。
6. **未改动**：轴承列表"替代品"查看弹窗（走 `/api/proxy/interchanges/{bearingId}`）保留；纠错管理页无需改动（`interchange` 字段随既有纠错流程走，审批后在替代品管理页人工落地）。

### v1.40.0 → v1.41.0（2026-10-17 仪表盘两栏布局 + "互联网新增"图改由扩展槽提供 + 图表固定高度）

1. **仪表盘改左右两栏布局**（`Views/Home/Index.cshtml`）：左栏 `col-lg-8` 竖排图表，右栏 `col-lg-4` 竖排「品牌 Top10 + 服务状态」（原为图表一行 + 品牌整行 + 服务状态独占一行）。窄屏（`< lg`）由 Bootstrap 自动堆叠为整行，手机端依次为 图、图、品牌 Top10、服务状态。
2. **图表固定高度**：canvas 外层加 `height:280px` 容器并设 `maintainAspectRatio:false`。修复存量问题——此前 `responsive:true` 且未关宽高比，`height="200"` 属性被忽略、高度随宽度按 2:1 膨胀（整行时约 585px 过高）。
3. **"互联网新增"图改由扩展槽提供**：共享视图左栏留 `ProSlot("home.trend.internet")`，**开源无 `Views/Ext/home.trend.internet.cshtml` → 只显示「人工新增」一张图**；启用互联网数据管线的部署由该分部注入「互联网新增」卡片（扩展侧新增）。共享 JS 只绘制人工图，并把绘图函数暴露为 `window.ofbMakeTrend(canvasId, bearingSeg, merchantSeg, interchangeSeg, isInternet)` 供扩展脚本复用。
4. **下钻沿用**：三组柱子（轴承/商家/替代品）点击仍按业务日历起点 + 对应来源跳转对应列表（`isInternet` 决定来源口径）。
5. 说明：不采用"读 `Features:SyncIntegration` 隐藏互联网图"的做法——那会在开源代码中重新引入 Sync 字样（v1.36.0 已代码级摘除），扩展槽是本规则下的正解。

### v1.39.0 → v1.40.0（2026-10-17 仪表盘"新增"拆两张图并按来源拆分 + 商家列表时间/来源筛选）

1. **修复仪表盘假数据**：`Views/Home/Index.cshtml` 原商家柱"本周"用 `总数/30` 估算、"本月"写死 0（仅"今日"为真值，自 2026-06-29 起一直如此）；现全部改用 API 真值。
2. **新增趋势拆两张图并按来源拆分**：原单图改为**两张同款分组柱图**——「互联网新增（爬虫/同步）」与「人工新增（平台维护 / 商家入驻）」，每张 x 轴为 今日/本周/本月，含 **轴承 / 商家 / 替代品** 三系列。数据取 API `bearings.internetAdded|manualAdded`、`merchants.internetAdded|onboarded`、`interchanges.internetAdded|manualAdded`；品牌、类型不上图（低频主数据）。
3. **三组柱子均可下钻**：读 API 顶层 `periods`（业务日历起点），点柱跳对应列表——
   - 商家柱：互联网图 → `?dateField=registered&dateFrom=<起点>&sourceFilter=internet`；人工图 → `?dateField=onboarded&dateFrom=<起点>`；
   - 轴承柱 → `/Data/Bearings?dateFrom=<起点>&sourceFilter=internet|noninternet`；
   - 替代品柱 → `/Interchanges?createdFrom=<起点>&sourceFilter=internet|noninternet`。
4. **卡片"已入驻"改真值**：`商家数量（已入驻/总数）` 的"已入驻"改用 API `merchants.onboardedCount`（`OnboardedAt` 非空），此前误用 `verifiedCount`（认证数），文案与数据不符。
5. **商家列表新增"入驻时间"列 + 时间/来源筛选**（`Views/Data/Merchants.cshtml`）：
   - 列：新增"入驻时间"（`onboardedAt`，可空），与"注册时间"列均可点击排序（`sortBy=onboardedat` / `registeredat`，默认最新在前）。
   - 筛选：时间字段下拉（注册时间/入驻时间）+ 起止日期（`dateField`/`dateFrom`/`dateTo`）+ 来源下拉（全部/仅互联网/仅非互联网，`sourceFilter`），并提供"清除筛选"按钮。
   - 分页与"全部/已认证/未认证"页签保留全部筛选与排序状态；空行 colspan 9→10。
6. **代理与视图模型透传**：`DataController.Merchants` 改为 `dateField/dateFrom/dateTo/sourceFilter/sortBy/sortOrder` 并映射为 API 的 `registeredFrom|onboardedFrom|internetSource|sortBy|sortOrder`；`MerchantListViewModel` 增对应字段；`MerchantItemDto` 增 `onboardedAt`。
7. **轴承列表与替代品列表加下钻筛选**（支撑对应柱子下钻）：
   - **轴承列表** `Views/Data/Bearings.cshtml`：筛选行加"创建时间 起~止 + 来源（全部/仅互联网/仅非互联网）"，表格新增"来源"与"创建时间"两列（`data-utc` 渲染）；`DataController.Bearings` 透传 `dateFrom/dateTo/sourceFilter` 为 API `createdFrom/createdTo/internetSource`；`BearingItemDto` 增 `dataSource`/`createdAt`。
   - **替代品管理页** `Views/Interchanges/Index.cshtml`：筛选行加"创建时间 起~止 + 来源"，`PageUrl` 保留；`InterchangesController.Index` 透传 `createdFrom/createdTo/sourceFilter`。
   - 两页均提供"清除时间/来源筛选"按钮。

### v1.38.0 → v1.39.0（2026-10-16 配置优先级修复：部署环境变量优先于扩展覆盖配置）

1. **修复"扩展覆盖配置盖过部署环境变量"**：`Program.cs` 里 `AddJsonFile("appsettings.pro.json", optional)` 在 `WebApplication.CreateBuilder`（已含环境变量源）**之后**加载，配置源后加者优先 → 镜像内该文件里的键（如各服务基地址）会**覆盖部署时注入的环境变量**，致使线上仍走镜像内默认值（现象：服务健康检查/相关面板连 `localhost` 默认地址失败）。现于其后追加一次 `AddEnvironmentVariables()`，确立优先级：**appsettings.json < 扩展覆盖配置 < 环境变量**。
2. **影响面**：开源基座不含 appsettings.pro.json，此调整对开源无副作用；对存在该覆盖文件的扩展部署，保证"部署环境变量始终生效"。
3. **配套**：扩展覆盖文件只应承载"镜像内默认值"，部署地址一律经环境变量注入（勿写死线上地址）。

### v1.37.0 → v1.38.0（2026-10-16 服务状态抽共享组件 + 登录页注释缺陷修复）

1. **新增共享组件** `ServiceStatusViewComponent`（`ViewComponents/ServiceStatusViewComponent.cs` + `Views/Shared/Components/ServiceStatus/Default.cshtml`）：渲染服务状态表格骨架 + 一段遍历脚本，`fetch('/Home/Status')` 后按返回的**键逐个生成行**（已知键映射 Api→API / Identity→Identity，未知键原样显示）。参数 `cssClass`（登录页 `status-table` / 仪表盘 `table`）与 `showDetails`（仪表盘三列 服务/状态/详情，登录页两列 服务/状态）。
2. **两处视图去重**：`Views/Account/Login.cshtml` 与 `Views/Home/Index.cshtml` 原来各自维护"静态行 + 硬编码 nameMap JS"，现统一改为 `@await Component.InvokeAsync("ServiceStatus", ...)`，删除重复的 `/Home/Status` 取数与逐行刷新 JS。
3. **修复登录页注释被当文本渲染的缺陷**：`Login.cshtml` 原把"死行删除"注释写成了 `{{-- … --}}`（Liquid 风格，非 Razor 注释），导致该行注释**直接显示在页面上**；本次随组件化一并移除。
4. **扩展方受益（零视图改动）**：扩展模块经 `IAdminModule.AddHealthServices` 追加的健康项由组件遍历自动生成行，登录页与仪表盘显示一致（内置项 + 扩展项）。

### v1.36.0 → v1.37.0（2026-10-13 商品审核出口 + 替代品三级治理）

1. **商品审核页新增** `Views/MerchantBearingReview/Index.cshtml`（+ `MerchantBearingReviewController`）：待审商品队列 + 通过/驳回（驳回填理由），数据源 API `/api/admin/merchant-bearings/pending|approve|reject`（权限 merchant.verify）；`HomeController` 仪表盘"待审商品"角标接真实计数（原写死 0）。
2. **替代品两级页新增**：`InterchangesController` + `Views/Interchanges/Index.cshtml`（平台管理：列表/筛选/新建选型号/编辑/删除）、`InterchangeReviewController` + `Views/InterchangeReview/Index.cshtml`（候选审核：通过/驳回附理由），数据源 API `/api/admin/interchanges*`（merchant.verify）。
3. **修复原失效的轴承列表"替代品"弹窗**：代理路由指向真实端点、字段名对齐（此前指向不存在的 `/api/interchanges/by-bearing/{id}` + JS 字段名错配，恒"加载失败"）。
4. **代理补齐**（Program.cs）：新增 `/api/proxy/admin-interchanges/*`（列表/待审/新建/编辑/删除/通过/驳回，7 条）与 `/api/proxy/admin-merchant-bearings/*`（待审/通过/驳回）代理；`_Layout` 信息管理组加"替代品管理/商品审核"入口（外组权限条件补 merchant.verify）。
5. **随共建**：商品在售状态列改用 API 下发的 `saleState`/`reviewStatus` 文案（与《15-数据状态设计 v1.2.0》一致）。

### v1.35.0 → v1.36.0（2026-10-07 开源化：Sync/Crawler 集成面代码级摘除）

1. **功能门控升级为代码摘除**：`Features:SyncIntegration` 开关连同被控代码整体移出开源仓——
   - 控制器删除：SyncController（ETL 同步）、ReviewController（同步数据审核）、MappingController（品牌/型号映射维护）整文件；HomeController 的 DataSources（数据源页）与 Crawler（爬虫触发页）action；DataController 的库存导入三动作（页面/模板下载/提交）；AuditLogController 的 `source=sync` 审计源分支。
   - 视图删除：`Views/Sync|Review|Mapping|Crawler/` 四目录、`Views/Data/ImportInventory.cshtml`、Merchants 页 Excel 导入弹窗与 JS。
   - 基础设施删除：Program.cs `SyncClient` HttpClient 注册与 `/api/proxy/excel/*`、`/api/proxy/etl/*` 代理端点；ServiceHealthService 的 Sync 探测项；_Layout 三处菜单块（同步审核/ETL 与爬虫/映射信息/库存导入）与 syncOn 门控变量；PermissionController 三个显示名（merchant.import/review.sync/sync.run）。
   - 配置删除：appsettings `Features` 节与 `ApiUrls:FindBearingsSync` 键；deploy 模板 `ApiUrls__FindBearingsSync` env。
2. **配套 API 摘除**（见 API《02 v1.46.0》）：/api/sync 端点组、import-context、config/reliability、SeedData 权限目录五键（merchant.import/mapping.view/mapping.manage/review.sync/sync.run）。
3. **保留说明**：商家 `DataSource=Crawler` 枚举为平台核心业务属性（互联网数据来源标记，展示与守卫逻辑在用）不摘；DashboardStats 去 Sync 聚合、仪表盘同步数据待处理卡删除，仅呈现本 API 自身统计。
### v1.34.0 → v1.35.0（2026-10 Sync 集成门控默认关闭）

1. **`Features:SyncIntegration` 默认值改 `false`**（v1.34.0 起为 true 默认、靠部署置 false 门控）：开源版部署（不含 Sync 数据管线）开箱即无 Sync 痕迹——
   - 菜单隐藏（_Layout syncOn 判定）与控制器 404 门控全覆盖：SyncController/ReviewController 整控制器、HomeController.Crawler、DataController 库存导入三动作；**本轮补漏**：MappingController 整控制器（品牌/型号映射维护）、AuditLogController 的 `source=sync` 审计源（404，identity/api 源不受影响）。
   - 至此 Sync 数据审核 / ETL 同步 / 数据爬虫 / 库存导入全链路在开源部署不可见、不可达。
2. **自用部署显式启用**：infra `apps/admin/deployment.yaml` env 注入 `Features__SyncIntegration: "true"`，测试服务器行为不变。

### v1.33.0 → v1.34.0（2026-09-29  Sync 集成门控）

1. **`Features:SyncIntegration` 功能开关（appsettings 默认 true，内部版零变化）**：公开版部署（无 Sync 数据管线）置 false 后——
   - 菜单隐藏："同步数据审核 / ETL 同步 / 数据爬虫 / 库存导入（含"库存管理"子标题）"（_Layout `syncOn` 判定，与权限并行）；
   - 控制器 404 门控：`SyncController`/`ReviewController` `OnActionExecuting` 整控制器 404，`HomeController.Crawler` 与 `DataController` 库存导入三动作（页面/模板下载/提交）404——防绕过菜单直达 URL。
2. **DataSources 三态化（修"Sync 宕机伪装暂无数据源"老缺陷）**：成功=数据数组；不可达/非2xx=**502 `sync_unreachable`**；门控关闭=**503 `sync_disabled`**。爬虫视图按错误码分支：unreachable 显示"Sync 服务不可用 + 重试按钮"，disabled 显示"本部署未启用 Sync 集成"。
3. **库存导入链路口径（对齐《02 v1.43.0》《BFF v1.10.0》）**：Admin 侧"库存导入"页保持直连 Sync `/api/inventory/import`（平台代办，符合"后台该调谁调谁"原则）；商家自助导入已改 BFF 编排、不再经 API。
4. **安全**：本文档历史版本中出现的 `Internal:ApiToken` 真实值已随公开库泄露，2026-09-29 起轮换废弃——新值只存 infra `secrets/real/`（api/mobile 两侧 Secret），仓库 appsettings 一律 REPLACE_ME；configMap 不再持有令牌。


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

1. **材料图片基址修复（真机验证暴露）**：新增配置 `Media:PublicBaseUrl`（默认 `https://<your-bff-domain>/media`，K8s env 可覆盖）。证照材料队列"查看原件"与入驻抽屉 `DOC_BASE`/`dLogo` 原拼 `ApiUrls:OpenFindBearingsApi`（K8s 内为集群内 svc 地址，浏览器不可达 → 图片 404 且 onerror 隐藏后卡片呈"不可点"），统一改公网媒体基址。
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
- v1.31.1 权限目录重排 UI 收口：① _Layout 菜单子项独立键（品牌=brand.view/类型=type.view/商家=merchant.view/库存导入=merchant.import/映射=mapping.view/ETL与爬虫=sync.run/权限页=permission.view/用户页=user.view/sync.review→review.sync）；② 按钮级显隐——数据四页与用户页的新增/编辑/删除/恢复/彻底删除/解除归属/分配角色/重置密码按钮包 PanelPerms.Has(User, 动作键)（无权限不可见，不再点了才 403），helper 新增 Models 旁 Helpers/PanelPerms.cs、_Layout 保留局部 HasPerm；③ 控制器守卫换新键并补 action 级（Users gate=user.view + Create/Restore=user.manage + ToggleStatus/Unlock/ResetPassword=user.ban + HardDelete=data.harddelete + PlatformRoles/Save=user.assign；Config Update=system.manage；PointsController gate=points.manage）；④ 用户页"显示已删除"勾选升级状态下拉（正常/已禁用=黑名单/已锁定/已删除/全部），分配角色弹窗复选可并存+「无」卸任项；⑤ 仪表盘第二行四张待办卡数字红色 text-danger；⑥ PermissionKeys 死枚举类删除、权限配置页展示名直读 API Description+组名前缀推导（废 37 键双 switch）；⑦ 用户管理操作 status 透传（禁用/解锁/重置/恢复/删除/新建回跳停在当前状态视图）+默认页签改回「全部」；⑧ 两步式删除——删除按钮仅出现在已禁用行（user.manage），控制器 fail-closed 守卫（不能删自己/唯一启用管理员拒删/校验不可达即拒），软删可在已删除视图恢复或彻底删除，确认文案警示业务数据不级联清理；⑨ 仪表盘待办数字条件红（>0 才挂 .todo-alert，深色主题亮红 #ff6b6b，0 安静用主题色）；⑩ 锁定链路闭环——UserItemDto 镜像 Identity 新增 IsLockedOut（5 次输错密码锁 15 分钟），状态列"已锁定"蓝徽章+操作列解锁按钮（user.ban，激活原孤儿 Unlock 动作）；⑪ 页签切换写回表单 hidden 与分页链接 tab 参数（修状态筛选/翻页弹回默认页签）+ 搜索 GET 表单去重 status hidden（双 name 致筛选失效）
- v1.30.0 密码体系改造：新建后台用户不再输入密码（Identity 自动用系统初始密码，弹窗提示"首登强制改密"）；重置密码弹窗改一键"重置为系统初始密码"（调 Identity reset-to-default，成功后成功提示回显密码值一次供转告本人）；初始密码单一事实源在 Identity 配置 Security:InitialUserPassword，Admin 侧零密码输入零存储
- v1.29.0 用户/角色页改版：用户列表角色列接通 API 平台角色（批量 platform-roles 端点合并，徽章显中文显示名）+ 全部/后台用户/App 用户三页签（当前页过滤，.ofb-tabs 公共皮肤移动端横滑）；角色管理显示 DisplayName 主显+英文标识副标、新建弹窗标识/显示名分离（标识前端预检英文正则）；平台角色弹窗目录带中文名；系统配置页签套用 .ofb-tabs；修复角色删除守卫小写 admin 恒不命中与 GetRoleDetail 基类强转派生类 500 两处存量缺陷；权限配置页修复（视图读 ViewBag.Items 与控制器强类型 Model 不通致清单恒空，改 @model 渲染+空态提示）；新建用户弹窗改版「新建后台用户」——平台角色必勾至少一个（Individual 不供勾选，App 用户走 app 自助注册），建号成功即调 API provision 预置业务行并授权，解「未登录过不能分配角色」死结；v1.29.1 四修：① 权限配置组标题去 bg-light 浅色硬类（深色主题白底不可读）；② 平台角色弹窗保存遇 404 自动走 provision 预置（老账号补角色闭环）；③ 弹窗目录排除 Individual（App 用户自助注册产生非后台分配）；④ 用户页默认页签改「后台用户」（菜单语义=后台账号管理）；v1.29.3 页签保持与禁用修复：用户页全部操作（新建/禁用启用/恢复/彻底删除/重置密码/解锁）与翻页搜索均携带 tab 回跳原页签；修复禁用/启用恒失败——原 PATCH 发空 body 而 Identity 端点要求 {enable} JSON 体必 415，改传显式目标态；v1.29.4 命名定稿：用户页第三页签「前端用户」（与「后台用户」对应），Individual 角色中文显示名定为「普通用户」（迁移 SQL/SeedData/徽章 fallback 三处同步，未发布直接改）
- v1.28.0 系统配置 tab 化改版：配置页由纵向多卡片改为 Bootstrap nav-tabs（站点设置/价格显示/数据同步/业务设置/积分任务，按常用序排列、未知组垫底、?tab= URL 记忆页签，配置与规则保存回跳均停留原 tab）；独立"积分任务"页并入为末位 tab（PointsController 删 Index 仅存 SaveRules 保存端点，Views/Points 与菜单项删除）；groupNames 补 Business=业务设置 映射（BusinessClock 时区偏移种子落此组）；积分规则拉取失败单独降级不阻塞配置 tab；深色主题修复：site.css 补 form-control disabled/readonly 主题化规则（Bootstrap 默认浅灰底在深色下文字不可见），弹窗配置键改 readonly 可选中复制
- v1.27.0 积分任务管理页（/Points，system.view 权限组）：赚分规则表格批量保存（分值/每日上限/连续阶梯/启停开关），代理 API /api/admin/points/rules，改完实时生效不发版；复选框隐藏 false 垫底保证停用可提交；合规红线（不可充值/提现/转让）页头标注。
- v1.25.0 商户管理页新增"解除归属"操作（POST /api/admin/merchants/{id}/detach，权限键 merchant.detach 迁移补插存量库）：认领商户清场回公海（商品/证照/成员/邀请/纠错全清，随爬取自然更新、可再认领），自建商户被 API 拒绝并透传原因；data-confirm-danger 二次确认；权限分组与中文名两处映射同步。
- v1.25.0 后台权限体系接线 API RBAC（三员分立轻量落地）：① 登录门禁——OIDC 回调后调 API GET /api/me/permissions，无面板角色（Admin/Operator/Auditor）即拒登，权限清单写 cookie 多值 claim；② Cookie 事件 30 分钟复核（SecurityStamp 模式），权限收回自动踢出；③ 菜单按 permission claim 动态渲染（Admin 角色全显示）；④ 13 个 Controller 挂 [PanelPermission] 动态策略（PanelPolicyProvider 按需生成）；⑤ 角色/权限管理页改代理 API /api/admin/roles*（权限点目录只读），本地 db_admin 僵尸表（admin_user_roles/admin_role_permissions）与种子整体删除；⑥ 用户页新增"平台角色"弹窗（by-auth 端点差量分配，自锁守卫禁移自己 Admin）。 |
- v1.25.0 解除归属按钮可用性动态化（与 API DetachMerchant 守卫严格同口径）：仅 status=Active 且（applicationMode=Claim 或 None+dataSource=Manual 提名已有）可点；其余置灰+title 说明（自建用删除/公海无归属可解）。API MerchantDto/映射补 dataSource 字段（v1.31.1）。

### v1.31.1（2026-09-26）

- 小屏适配全站收口：`.table th`/`.btn` 禁换行 + 按钮禁 flex 压缩（搜索图标按钮不再缩成蓝点）；用户/商家/品牌/轴承/类型/系统配置表格统一包 `table-responsive`（列取自然宽横向滚动，杜绝 CJK 逐字竖排）；次要列小屏隐藏（用户邮箱、商家英文名/公司/类型、审计资源/IP、配置描述/说明——`d-none d-md-table-cell`）
- 按钮小屏图标化：全站行内操作按钮文字包 `d-none d-sm-inline`，<576px 只留图标并补 `title`；无图标按钮按语义补 FontAwesome 图标（编辑/恢复/删除/解锁/重置密码/分配角色/解除归属/在售商品/替代品/在售商家/权限配置/下架等）
- 深色主题白底根治：启用 Bootstrap 5.3 原生 `data-bs-theme`（_Layout 内联引导脚本防白闪 + site.js 切换同步 + site.css 组件变量对齐项目配色），分配角色/新建用户弹窗 list-group、alert、dropdown、form-control 等全族组件随主题自动换底
- 商家列表类型列中文化（@functions MerchantTypeName，与入驻审批页同映射）；新建用户密码提示从大块 alert 改为 form-text 小字（主流样式）
- 角色体系收口：Individual 显示名定名"注册用户"（与"后台用户"对偶、中性描述来源；SeedData+文案多处同步）；DeleteRole 内置守卫从硬编码名字名单改为读 Role.IsSystem 字段（字段设计初衷接通，GetRolesQueryHandler 死私法删除）；分配角色弹窗内置角色显示"内置"徽章（消费 isSystemRole）；角色新建弹窗补"描述"输入框（API 一直支持 description，UI 此前无入口且恒传 null）

---

## 本版增量（v1.32.0）：成就管理 + 商城三区（承 API 11-积分体系 v2.2.0~v2.5.0）

- 成就管理页（system.view 菜单/system.manage 编辑）：目录分组列表+编辑模态框（分值/阈值/称号/启停），数据源 API /api/admin/achievements。
- 商城管理页重构三区页签：平台权益目录（价格/闪购窗口 UTC↔本地浏览器转换/库存/上下架编辑）、挂礼待审（缩略图 data-utc 时间/定档价输入≤封顶/通过、驳回 showPrompt 原因）、托管订单（物流态徽章+争议退款 showPrompt，未结算才可退）。
- 权限复用零新键：目录 system.*、挂礼审核 merchant.verify、争议退款 merchant.manage；按钮显隐 PanelPerms.Has 三层防线。

---

## 本版增量（v1.33.0）：系统配置页"商家任务"tab（承 API 11-积分体系设计 v2.6.0）

- 系统配置页新增末位"商家任务"页签（points.manage）：集体任务定义批量编辑表（任务名/描述/指标/目标值/周期/奖励对象/分值/排序/启停，
  TaskKey 只读锚定台账不可改）+ 新建任务表单卡片；
  数据源 GET/POST/PUT /api/admin/points/merchant-tasks，保存 POST /Points/SaveMerchantTasks、新建 POST /Points/CreateMerchantTask
  （均回跳 ?tab=merchattasks）。达标即结算由 API 每小时 Job 执行，Admin 只管定义。
- 配置键更名（未发布迁移种子直接改）：Business.GuildPremiumOnSaleMin/GuildGoldOnSaleMin/GuildGoldTreasuryMin
  -> Business.Merchant*，业务设置 tab 行名随键自动更新。

---

## 本版增量（v1.37.0）：商品审核 + 替代品管理/审核（承 API《02 v1.48.0》《16-替代品治理设计 v1.0.0》）

| 页面 | 控制器/视图 | 功能 | 数据源 API | 权限 |
|---|---|---|---|---|
| 商品审核 | MerchantBearingReviewController / Views/MerchantBearingReview | 待审队列 + 通过 / 驳回（填理由） | /api/admin/merchant-bearings/pending\|approve\|reject | merchant.verify |
| 替代品管理 | InterchangesController / Views/Interchanges | 列表 / 筛选（关键字 + 创建时间 + 来源）/ 新建（选源·目标型号）/ 编辑 / 删除 | /api/admin/interchanges（GET/POST/PUT/DELETE） | interchange.view（读）/ interchange.manage（写） |
| ~~替代品审核~~ | ~~InterchangeReviewController / Views/InterchangeReview~~ | **已于 v1.42.0 移除**（替代品字典化：无候选流即无审核队列） | — | — |

- 菜单：信息管理组新增"商品审核/替代品管理"入口（外组权限条件补 merchant.verify）；**v1.42.0 起"替代品审核"入口删除**；**v1.43.0 起"替代品管理"入口改由 `interchange.view` 控制**（本节权限列为历史值，已由 v1.43.0 更新）。
- 修复：轴承列表"替代品"查看弹窗（原代理路由与字段名错配，恒加载失败）。
- 商品列表在售状态列改按 API `saleState`/`reviewStatus` 映射展示。

---

## 本版增量（v1.42.0）：替代品字典化——删除审核页、管理页去审核态（承 API《02 v1.52.0》《16-替代品治理设计 v2.0.0》）

| 对象 | 变更 |
|---|---|
| `InterchangeReviewController` + `Views/InterchangeReview/` | 删除（含 `_Layout` 侧边栏"替代品审核"入口）：替代关系回归平台维护的主数据（字典），不再有"商家报候选 → 平台审核生效"队列 |
| `Views/Interchanges/Index.cshtml` | 删"审核状态"筛选下拉、"状态"列（含驳回理由）、"提出商户"列——表格列 9→7（管理员含操作列 8）；页头说明改为平台维护口径 + 纠错核实 |
| `InterchangesController` | `Index` 去掉 `reviewStatus` 入参；行 DTO 删 `ReviewStatus`/`ReviewStatusLabel`/`ReviewComment`/`ProposedByMerchantId`/`ProposedByMerchantName` |
| `Program.cs` 代理 | 删 `/api/proxy/admin-interchanges` 的 **GET 列表**、**GET /pending**、**POST /{id}/approve**、**POST /{id}/reject** 四条（保留 新建/编辑/删除；列表页服务端直取 API） |
| `HomeController` 降级 JSON | `interchanges` 占位对象删 `pendingCount`（对齐 API 字段裁剪） |
| 未改动 | 轴承列表"替代品"查看弹窗代理（`/api/proxy/interchanges/{bearingId}`）保留；纠错管理页不变（`interchange` 字段走既有纠错流程，采纳后在替代品管理页人工落地关系） |

> 数据来源：权威网站抓取（Sync staging 审核后入库，进 API 即生效）或平台人工维护；商家/用户经"纠错"反馈，平台核实后维护。
> 迁移（API 侧）：`RemoveInterchangeReview`（删三列与索引，先清除历史未生效候选行）。

---

## 本版增量（v1.43.0）：替代关系独立权限键（承 API《02 v1.53.0》《16-替代品治理设计 v2.0.1》）

| 对象 | 变更 |
|---|---|
| 替代品管理页判权 | `InterchangesController` 类特性 `[PanelPermission("merchant.verify")]` → `[PanelPermission("interchange.view")]` |
| 写按钮判权 | `Views/Interchanges/Index.cshtml`：`PanelPerms.Has(User, "interchange.manage")` 控制新建/编辑/删除按钮（原借用 `merchant.verify`） |
| 菜单判权 | `Views/Shared/_Layout.cshtml`：「替代品管理」入口由 `interchange.view` 控制（原 `merchant.verify`） |
| 权限分组与显示名 | `PermissionController`：新增 `interchange` → 「数据管理」分组映射；`interchange.view`/`interchange.manage` 显示名回退 |
| API 端点 | `GET /api/admin/interchanges` 需 `interchange.view`；`POST/PUT/DELETE /api/admin/interchanges[/{id:guid}]` 需 `interchange.manage` |
| 权限目录 | 32→34 键；迁移（API 侧）`AddInterchangePermissions` 幂等补插 `interchange.view`/`interchange.manage` 并给 Admin/Operator/Auditor 授权（Operator 18→20、Auditor 11→12） |

> 说明：本版仅调整替代品管理相关的权限归属，页面功能与数据源不变；v1.37.0 增量表中替代品管理的权限列（merchant.verify）为历史记录，现状以本表为准。

---

## 本版增量（v1.46.0）：寻货治理「已删除」标记

| 对象 | 变更 |
|---|---|
| 寻货列表 | `Views/Sourcing/Index.cshtml` 型号列在单号后按 `IsDeleted` 渲染 `已删除` 徽标 |
| 寻货详情 | 详情弹窗标题行状态徽标后按 `d.isDeleted` 渲染 `已删除` 徽标 |
| 行 DTO | `SourcingAdminItemDto` 新增 `IsDeleted` |
| API 侧（本版背景） | `GET /api/admin/sourcing/demands` 改传 `includeDeleted: true` 并输出 `isDeleted`（详情同步输出） |

> 说明：本版只做治理可见性标注，寻货页其余功能与判权不变。

---

## 本版增量（v1.49.0）：型号线索待建档

| Admin 对象 | 上游端点 | 权限 |
|---|---|---|
| `GET /Data/BearingLeads?status=&keyword=&page=&pageSize=` | `GET /api/admin/bearing-leads` | `bearing.view` |
| `POST /Data/IgnoreLead/{id}` | `POST /api/admin/bearing-leads/{id}/ignore` | `bearing.edit` |
| `POST /Data/RestoreLead/{id}` | `POST /api/admin/bearing-leads/{id}/restore` | `bearing.edit` |
| `GET /Data/Bearings?newPartNumber=&newBrand=`（预填，不新增端点） | `GET /api/admin/bearings` | `bearing.view` |

> 三态由 API 判定：待建档（未忽略且库中无同型号）、已忽略（`isIgnored`）、已入库（库中已有同型号，忽略大小写）。「去建档」跳轴承列表并自动打开新建弹窗，保存后线索自动转已入库。

---

## 本版增量（v1.49.2）：轴承主图维护

| Admin 对象 | 上游端点 | 权限 |
|---|---|---|
| `POST /Data/EditBearing/{id}`（文本字段） | `PUT /api/admin/bearings/{id:guid}` | `bearing.edit` |
| `POST /Data/EditBearing/{id}`（3D/2D 图片，multipart） | `POST /api/admin/bearings/{id:guid}/images` | `bearing.edit` |

> 同一弹窗一次提交：先 PUT 文本，成功后再按需 POST 图片（未选文件且未勾清空则跳过）。图片失败不回滚文本，提示改为"轴承信息已更新，但图片更新失败: 原因"。
> 说明：本版只新增读取与忽略/恢复，不引入线索状态机迁移、不新增权限键，也不做自动入库。