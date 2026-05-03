# OA 五大常用功能实施路线图

## 当前进度

| Step | 功能 | 状态 | 后端测试 | 交付日期 |
|------|------|------|---------|---------|
| 1 | 通讯录 | ✅ 已完成 | 11 | 2026-04-18 |
| 2 | 公告通知 | ✅ 已完成 | 12 | 2026-04-18 |
| 3 | 考勤打卡（含日历 + 多班次） | ✅ 已完成 | 16 | 2026-04-19 |
| 4 | 待办中心 | ✅ 已完成 | 6 | 2026-04-19 |
| 5 | 会议室预订 | ✅ 已完成 | 12 | 2026-04-19 |

**✅ 5 步全部完成** — OA 当前测试覆盖 **90 个**（UMC 79 个）

**增强项**：
- Step 3 扩展：月历视图 + 4 种子班次 + 用户班次分配
- Step 4 扩展：会议室完成后联动显示今日会议
- 全局改造：统一 `createAuthedHttp` 工厂 + axios 响应拦截器自动转换北京时间
- **持久化迁移（2026-04-19）**：OA 后端从 InMemoryDataStore 迁移到 SQLite (EF Core 8)，数据可持久化；90 个测试保留通过

**文档关系**：
- `ROADMAP.md`（本文件）— 分步实施方案与细节（数据表、API、流程、验收标准）
- `TODO.md` — 整体实现进度清单（跨 UMC/OA）
- `CLAUDE.md` — 系统功能规格（架构、数据模型、接口清单）
- `DECISIONS.md` — 关键架构决策记录

## Context

OA 已实现审批流、职员管理、SSO、用户/权限。现按推荐顺序分 5 步实现常用 OA 功能。
**铁律**：每一步完成后必须测试验收通过（dotnet test + tsc --noEmit + 人工浏览器验证）才能进入下一步。

每步交付标准：
- 后端：InMemory 数据 + 种子 + 集成测试 + Swagger 可调
- 前端：TypeScript 零错误 + 页面可正常渲染/操作
- 关键流程：人工测试过一遍

---

## Step 1：通讯录（Contact Book）— 预计最简单

### 目标
员工通过侧边栏打开通讯录，按部门组织查看同事信息，支持搜索/拨号/发邮件。

### 功能点
- 左侧：部门树（章鱼科技 / 海星科技 两家公司的组织架构）
- 右侧：员工列表/卡片（头像、姓名、部门、职位、手机、邮箱）
- 顶部搜索框：姓名/手机号/邮箱模糊搜索
- 员工卡片点击 → 详情抽屉（完整信息 + 一键拨号/发邮件/复制手机号）
- 导出部门通讯录为 Excel（可选，后续）

### 数据表
**无需新增表**。直接使用：
- OA `SyncUsers`（已有，含 umcUserId、userName、nickName、email、phoneNumber、avatar）
- UMC 部门树（OA 本地缓存或实时拉取）

**新增：OA 本地部门缓存**（用来组织通讯录部门树）：

| 表 | 字段 | 说明 |
|---|---|---|
| `OaDept` | deptId, parentId, deptName, companyName, orderNum | 从 UMC 同步的部门信息 |
| `OaUserDept` | userId(UmcUserId), deptId, isPrimary | 用户-部门关联（从 UMC UserSync 扩展） |

**SyncUser 扩展**：增加 `DeptIds`、`PostNames` 两个字段（从 UMC UserSync Webhook 推送时填充）

### 流程
```
[UMC 部门/用户变更]
    ↓ Webhook 推送（HMAC-SHA256）
[OA 接收同步] → OaDept / OaUserDept / SyncUser 本地缓存
    ↓
[员工打开通讯录]
    ├→ 加载 OaDept 树 → 渲染左侧
    └→ 按选中部门加载 SyncUser → 渲染右侧卡片
```

### API 接口
```
GET    /api/contact/dept/tree              部门树
GET    /api/contact/users?deptId=&keyword= 用户列表（支持部门筛选 + 关键字搜索）
GET    /api/contact/user/{umcUserId}       用户详情
POST   /api/contact/dept/sync              UMC 推送部门同步（HMAC 签名）
```

### 后端文件
```
OA.Api/Controllers/ContactController.cs     通讯录查询
OA.Api/Controllers/DeptSyncController.cs    接收 UMC 部门同步
OA.Api/Services/ContactService.cs           业务逻辑
OA.Api/DTOs/ContactDto.cs
OA.Api/InMemory/OAInMemoryDataStore.cs      扩展 OaDept / OaUserDept + 种子
```

**UMC 端需要补充**：
```
UMC.Api/Services/DeptSyncService.cs         部门推送服务（参考 UserSyncService）
DeptController 的 CRUD 操作后触发 DeptSyncService
```

### 前端
```
OA.Web/src/api/contact/types.ts + index.ts
OA.Web/src/views/contact/index.vue          通讯录主页
OA.Web/src/views/contact/components/UserCard.vue    员工卡片
OA.Web/src/views/contact/components/UserDetail.vue  详情抽屉
OA.Web/src/router/index.ts                  新增 /contact 路由
OA.Web/src/layouts/index.vue                侧边栏新增「通讯录」菜单（所有人可见）
```

### 验收标准
- [ ] 后端测试：ContactFlowTests（5+ 测试：部门树、用户列表、按部门筛选、关键字搜索、详情）
- [ ] 部门同步 HMAC 验证测试（正确/错误签名）
- [ ] 前端：打开 /contact 能看到两家公司树 + 员工卡片
- [ ] 搜索"张"能过滤出张三
- [ ] 点击卡片打开抽屉能看到完整信息 + 一键拨号/复制

---

## Step 2：公告通知（Announcement）

### 目标
UMC 管理员发布公告 → 自动推送到 OA → OA 首页和公告页展示 + 已读追踪。

### 功能点

**UMC 端**：
- 复用已有 NoticeController（CRUD）
- 发布/编辑时触发 Webhook 推送到 OA

**OA 端**：
- 公告列表页（可筛选类型：通知/公告/紧急）
- 公告详情页（富文本展示）
- 已读/未读状态（每用户独立）
- 首页公告卡片（最新 5 条）
- 未读红点提醒

### 数据表

**UMC**：复用已有 Notice 表（无需改动）

**OA 新增**：

| 表 | 字段 | 说明 |
|---|---|---|
| `OaNotice` | noticeId, title, content, noticeType, priority, publisher, publishTime, source(umc/oa), status | 镜像 UMC 公告 + OA 本地公告 |
| `OaNoticeRead` | id, noticeId, userId(UmcUserId), readAt | 用户已读记录 |

**noticeType**：1=通知（普通）2=公告（重要）3=紧急（置顶 + 红色）
**priority**：0-10 排序权重

### 流程
```
[UMC 管理员发布公告]
    ↓ NoticeController.Create/Update
    ↓ Webhook POST /api/notice/sync（HMAC 签名）
[OA 接收] → 存入 OaNotice
    ↓
[OA 用户访问首页]
    ├→ 加载最新 5 条公告
    ├→ 查 OaNoticeRead 判断未读
    └→ 点击公告 → 标记已读 → 显示详情
```

### API 接口
```
UMC 端：
POST   /api/system/notice/publish          发布（触发推送）

OA 端：
POST   /api/notice/sync                    接收 UMC 公告（HMAC 签名）
GET    /api/notice/list?type=&status=      公告列表（含已读状态）
GET    /api/notice/{id}                    详情 + 自动标记已读
GET    /api/notice/unread/count            未读数量
PUT    /api/notice/{id}/read               手动标记已读
GET    /api/notice/latest                  首页最新 5 条
```

### 后端文件
```
OA.Api/Controllers/NoticeController.cs        查询接口
OA.Api/Controllers/NoticeSyncController.cs    接收 UMC 推送（类似 UserSync）
OA.Api/Services/NoticeService.cs              已读逻辑
OA.Api/DTOs/NoticeDto.cs
OA.Api/InMemory/OAInMemoryDataStore.cs        扩展 OaNotice / OaNoticeRead + 种子（3-5条）

UMC.Api/Services/NoticeSyncService.cs         新建（参考 UserSyncService）
UMC.Api/Controllers/NoticeController.cs       修改：CRUD 后触发同步
```

### 前端
```
OA.Web/src/api/notice/types.ts + index.ts
OA.Web/src/views/notice/list/index.vue         公告列表
OA.Web/src/views/notice/detail/index.vue       公告详情
OA.Web/src/views/home/index.vue                修改首页增加「最新公告」卡片 + 未读徽标
OA.Web/src/router/index.ts                     新增 /notice/list 和 /notice/:id
OA.Web/src/layouts/index.vue                   侧边栏新增「公告中心」菜单 + 未读红点
```

### 验收标准
- [ ] 后端测试：NoticeSyncTests（HMAC 正确/错误、新增/更新、已读标记）
- [ ] UMC 发布公告 → 浏览器 F12 看到 Webhook 请求 → OA 接收成功
- [ ] OA 首页看到公告卡片 + 未读红点
- [ ] 点击公告 → 详情 → 刷新后红点消失
- [ ] 类型筛选正确

---

## Step 3：考勤打卡（Attendance）

### 目标
员工每天上下班打卡，系统记录并自动计算状态（正常/迟到/早退/缺勤）。忘记打卡通过审批流补卡。

### 功能点

**员工端**：
- 首页考勤卡片：今日打卡状态 + 上下班打卡按钮
- 我的考勤：月度考勤表格（日期、上班时间、下班时间、状态、工时）
- 补卡申请（打开审批模板「补卡审批」，关联日期 + 时段 + 原因）

**管理员端**：
- 考勤规则配置（上班时间、下班时间、迟到阈值、打卡 IP 白名单）
- 考勤统计（月度按部门/员工统计：迟到次数、缺勤次数、平均工时）
- 异常考勤列表（迟到/早退/缺勤）

### 数据表

| 表 | 字段 | 说明 |
|---|---|---|
| `OaAttendanceRule` | id, name, workStartTime(09:00), workEndTime(18:00), lateThresholdMin(15), earlyLeaveThresholdMin(30), ipWhiteList, status | 考勤规则（单条默认规则，后期可按部门/角色多条） |
| `OaAttendance` | id, userId, date(2026-04-18), checkInTime, checkOutTime, checkInIp, checkOutIp, checkInStatus(normal/late/missing), checkOutStatus(normal/early/missing), workHours, createTime | 每日打卡记录（userId + date 唯一） |
| `OaAttendanceFix` | id, attendanceId, approvalId, type(checkIn/checkOut), fixTime, reason, status | 补卡记录（审批通过后自动更新 OaAttendance） |

**状态枚举**：
- `normal` 正常
- `late` 迟到（上班超过 workStartTime + lateThreshold）
- `early` 早退（下班早于 workEndTime - earlyLeaveThreshold）
- `missing` 未打卡

### 流程

**正常打卡**：
```
[员工点击「上班打卡」]
    ↓ POST /api/attendance/check-in
    ↓ 校验 IP 白名单（可选）
    ↓ 查规则 → 计算状态（normal/late）
    ↓ 写入 OaAttendance（date=today, userId=当前用户）
    ↓ 返回打卡成功 + 状态
```

**补卡流程**：
```
[员工发现漏打卡]
    ↓ 发起补卡审批（预置「补卡审批」模板，表单：日期/时段/原因）
    ↓ 审批通过后
    ↓ OA 自动处理：更新 OaAttendance + 创建 OaAttendanceFix 记录
    ↓ 状态改为 normal（带补卡标记）
```

### API 接口
```
员工端：
POST   /api/attendance/check-in           上班打卡
POST   /api/attendance/check-out          下班打卡
GET    /api/attendance/today              今日打卡状态
GET    /api/attendance/mine?month=        我的月度考勤
POST   /api/attendance/fix-request        申请补卡（内部会创建审批实例）

管理员端：
GET    /api/attendance/rule               考勤规则
PUT    /api/attendance/rule               修改规则
GET    /api/attendance/stats?month=&deptId=  部门月度统计
GET    /api/attendance/abnormal?month=    异常列表
```

### 后端文件
```
OA.Api/Controllers/AttendanceController.cs
OA.Api/Services/AttendanceService.cs        核心规则计算、补卡
OA.Api/DTOs/AttendanceDto.cs
OA.Api/InMemory/OAInMemoryDataStore.cs      扩展三张表 + 种子（默认规则 + 本月几条打卡记录）

ApprovalService.cs                          修改：审批通过后通过事件触发补卡逻辑
                                            或 AttendanceService 监听审批通过事件
```

**新增补卡模板**（种子数据）：
```
TemplateCode: attendance_fix
FormSchema: { fixDate(date), fixType(select: checkIn/checkOut), fixTime(time), reason(textarea) }
Nodes: 直属主管审批 → HR 确认
```

### 前端
```
OA.Web/src/api/attendance/types.ts + index.ts
OA.Web/src/views/attendance/mine/index.vue        我的考勤（月历 + 表格）
OA.Web/src/views/attendance/stats/index.vue       考勤统计（oa_admin）
OA.Web/src/views/attendance/rule/index.vue        规则配置（oa_admin）
OA.Web/src/views/home/index.vue                   修改首页：考勤打卡卡片
OA.Web/src/router/index.ts                        新增 3 条路由
OA.Web/src/layouts/index.vue                      侧边栏「考勤」目录
```

### 验收标准
- [ ] 后端测试：AttendanceFlowTests
  - 打卡正常 → 状态 normal
  - 打卡迟到（模拟当前时间 >09:15）→ 状态 late
  - 重复打卡 → 返回「已打卡」
  - 未打卡 → 月度列表显示 missing
  - 补卡审批通过 → missing → normal + 有 Fix 记录
- [ ] 前端：首页卡片显示「今日未打卡」+ 按钮可打卡
- [ ] 打卡后状态更新为「已打卡 09:02 正常」
- [ ] 我的考勤看到整月记录
- [ ] 补卡审批走完流程后考勤状态更新

---

## Step 4：待办中心（Todo Center）

### 目标
聚合首页，让用户一眼看到所有待办事项：待审批 + 未读公告 + 今日考勤 + 会议提醒（Step 5 完成后）。

### 功能点

**工作台首页重构**：
- 顶部欢迎卡片（用户头像、问候语、今日日期）
- 4 个数据卡片：待我审批（数量）+ 未读公告（数量）+ 今日考勤（状态）+ 今日会议（数量）
- 「快捷入口」区域：发起申请/打卡/会议预订/通讯录
- 「我的待办」列表（聚合多源）：每条显示类型图标 + 标题 + 发起人 + 时间 + 操作按钮

### 数据表
**无新增表**。聚合查询现有数据：
- 待审批 → Approval（status=pending 且 approver=me）
- 未读公告 → OaNotice left join OaNoticeRead
- 考勤 → OaAttendance（today）
- 会议 → OaMeetingBooking（today 且参与）

### 流程
```
[用户登录后打开首页]
    ↓ GET /api/dashboard/summary     数据卡片（计数）
    ↓ GET /api/dashboard/todos       聚合待办列表（带类型过滤）
    ↓ 渲染工作台
```

### API 接口
```
GET    /api/dashboard/summary         首页卡片数据（各模块计数）
GET    /api/dashboard/todos?type=     待办列表（type=all/approval/notice/meeting）
```

**响应示例**：
```json
{
  "summary": {
    "pendingApprovals": 3,
    "unreadNotices": 2,
    "todayAttendance": { "checkedIn": true, "status": "normal" },
    "todayMeetings": 1
  },
  "todos": [
    { "type": "approval", "id": 5, "title": "张三的请假申请", "subtitle": "待您审批", "time": "2小时前", "link": "/approval/pending" },
    { "type": "notice", "id": 10, "title": "国庆放假通知", "subtitle": "未读公告", "time": "昨天", "link": "/notice/10" }
  ]
}
```

### 后端文件
```
OA.Api/Controllers/DashboardController.cs    聚合接口
OA.Api/Services/DashboardService.cs          聚合业务逻辑（调各 Service）
OA.Api/DTOs/DashboardDto.cs
```

### 前端
```
OA.Web/src/api/dashboard/index.ts
OA.Web/src/views/home/index.vue              重构首页（替换现有简单首页）
```

### 验收标准
- [ ] 后端测试：DashboardTests（summary 聚合正确、todos 按 type 过滤）
- [ ] 前端首页显示 4 个卡片 + 数字准确
- [ ] 点击卡片 → 跳转对应页面（已存在的路由）
- [ ] 待办列表含各类型，操作按钮可点击

---

## Step 5：会议室预订（Meeting Room）

### 目标
员工查看会议室 → 选时间段 → 预订（冲突检测）→ 参会人接收通知。管理员配置会议室。

### 功能点

**员工端**：
- 会议室列表（按容量/位置筛选）
- 日历视图（按天/周）：每个会议室一行，显示所有已预订时段
- 点击空白时段 → 新建预订弹窗（标题、时间段、参会人、说明）
- 我的预订：即将召开 + 历史
- 取消预订（仅预订人 + 未开始）

**管理员端**：
- 会议室 CRUD（名称、容量、位置、设备配置、图片）
- 查看所有预订

### 数据表

| 表 | 字段 | 说明 |
|---|---|---|
| `OaMeetingRoom` | id, name, capacity, location, equipment(JSON:["投影","视频会议"]), description, imageUrl, status | 会议室 |
| `OaMeetingBooking` | id, roomId, title, userId, userName, startTime, endTime, description, attendees(JSON:[userId..]), status(confirmed/cancelled), createTime | 预订记录 |

### 流程

**预订**：
```
[员工看日历 → 点空白时段]
    ↓ 弹窗填写预订信息 → 选参会人
    ↓ POST /api/meeting/booking
    ↓ 后端冲突检测（同房间 + 时间段重叠）
    ├ 冲突 → 返回 409
    └ 无冲突 → 保存 + 通知参会人（站内信，后期接邮件）
    ↓ 返回成功
```

**取消**：
```
[预订人点击取消]
    ↓ 校验：必须是预订人 + status=confirmed + startTime > now
    ↓ PUT /api/meeting/booking/{id}/cancel
    ↓ 通知参会人
```

### API 接口
```
会议室（管理员）：
GET    /api/meeting/room/list              会议室列表
POST   /api/meeting/room                   新建
PUT    /api/meeting/room                   修改
DELETE /api/meeting/room/{id}              删除（有未来预订则拒绝）

预订（员工）：
GET    /api/meeting/room/{id}/calendar?date=  某会议室日历（一天或一周）
POST   /api/meeting/booking                预订
PUT    /api/meeting/booking/{id}/cancel    取消
GET    /api/meeting/booking/mine           我的预订
GET    /api/meeting/booking/today          今日所有预订（用于 Dashboard）
```

### 后端文件
```
OA.Api/Controllers/MeetingController.cs
OA.Api/Services/MeetingService.cs           冲突检测 + 通知
OA.Api/DTOs/MeetingDto.cs
OA.Api/InMemory/OAInMemoryDataStore.cs      扩展两张表 + 种子（3 间会议室 + 几条预订）
```

### 前端
```
OA.Web/src/api/meeting/types.ts + index.ts
OA.Web/src/views/meeting/room/index.vue          会议室列表（CRUD，oa_admin）
OA.Web/src/views/meeting/calendar/index.vue      预订日历（所有人）
OA.Web/src/views/meeting/mine/index.vue          我的预订
OA.Web/src/views/meeting/components/BookingForm.vue  预订表单
OA.Web/src/router/index.ts                       新增路由
OA.Web/src/layouts/index.vue                     侧边栏「会议」目录
```

### 验收标准
- [ ] 后端测试：MeetingBookingTests
  - 正常预订成功
  - 同房间同时间段冲突 → 409
  - 非预订人取消 → 403
  - 已开始的预订不能取消
  - 删除有预订的会议室 → 失败
- [ ] 前端：日历视图正确显示已预订时段
- [ ] 点击空白 → 弹窗 → 提交 → 日历实时更新
- [ ] 我的预订能取消

---

## 全局约定

### 测试编写规则
- 命名：`被测方法_场景_预期结果`
- 集成测试：通过 OATestFactory + HttpClient 调用真实端点
- 每 Step 至少 6-10 个核心测试

### 前端约定
- 所有 API 函数放 `src/api/{模块}/`
- 所有类型放对应 `types.ts`
- 页面放 `src/views/{模块}/`
- 菜单加到 `layouts/index.vue` + 路由加到 `router/index.ts`

### 验收流程
每步完成后必须：
1. `dotnet test`（OA）全部通过
2. `npx tsc --noEmit` 零错误
3. 启动 Aspire，浏览器打开验收主要流程
4. 确认通过后才开始下一步

### 更新文档
每步完成：
- 更新 `TODO.md` 标记已完成
- 更新 `CLAUDE.md` 的 OA 功能清单

---

## 预计工作量

| Step | 复杂度 | 后端文件 | 前端文件 | 测试数 |
|------|--------|----------|----------|--------|
| 1 通讯录 | 中 | 5 | 4 | 6-8 |
| 2 公告通知 | 中 | 6 | 5 | 8-10 |
| 3 考勤打卡 | 高 | 5 | 5 | 10-12 |
| 4 待办中心 | 低 | 3 | 1 | 4-6 |
| 5 会议室预订 | 高 | 4 | 6 | 10-12 |

**从 Step 1 开始实施。本轮仅输出方案，等你确认后再开始写代码。**

---

## 附录 A：OA 持久化迁移（InMemory → SQLite，2026-04-19）

### 背景
五步完成后，OA 一直用 `OAInMemoryDataStore`（Singleton）保存 20 个实体。
应用重启数据丢失，不方便本地持久化开发和演示。参照 UMC 的 SQLite 实现完成迁移。

### 改动
- **新增 `OctopusOA.Api/Persistence/`**：`Entities.cs` + `OaDbContext.cs` + `OaDbSeeder.cs`
- **NuGet**：`Microsoft.EntityFrameworkCore` 8.0.15 + `Microsoft.EntityFrameworkCore.Sqlite` + `Design`
- **DI 生命周期**：Singleton → Scoped（跟随 DbContext）
- **7 个 Service + 5 个 Controller** 全部改为注入 `OaDbContext`
- **补卡联动**：ApprovalService 的 `OnApproved` 事件订阅移除，改为通过 `IServiceProvider.GetRequiredService<AttendanceService>()` 直接调用 `HandleApprovalFix`
- **测试基础设施**：`OATestFactory` 改为共享 SQLite 内存连接 `Data Source=:memory:;Cache=Shared`
- **删除** `OctopusOA.Api/InMemory/` 目录

### 数据库文件
- **开发**：`OctopusOA.Api/octopus_oa.db`（启动时 `EnsureCreated` + `OaDbSeeder.Seed`）
- **测试**：共享内存 SQLite
- 删除 db 文件重启 = 种子数据重建

### 表结构
20 个表（与实体一一对应，表名 `oa_*` 前缀）：
```
oa_sync_user / oa_dept / oa_user_dept
oa_workflow_template / oa_workflow_node
oa_approval / oa_approval_record
oa_employee / oa_employee_education / oa_employee_work / oa_employee_family / oa_employee_emergency
oa_notice / oa_notice_read
oa_attendance_rule / oa_user_shift / oa_attendance / oa_attendance_fix
oa_meeting_room / oa_meeting_booking
```

### 关键技术点
- **JSON 列**：`SyncUser.OaRoles` 用 `ValueConverter<List<string>, string>` 映射到 TEXT（参考 UMC OidcClient 模式）
- **唯一索引**：`oa_attendance` 对 `(UmcUserId, Date)` 建唯一索引
- **EF 表达式树限制**：含副查询的 `.Select(lambda)` 都先 `.ToList()` 再内存映射
- **ID 生成**：`.ValueGeneratedOnAdd()`，种子数据手动指定 ID

### 验证
- ✅ `dotnet test`：90/90 通过（不改测试逻辑，已有测试无缝适配）
- ✅ Aspire 启动，前端所有页面行为与迁移前一致
- ✅ 重启后数据保留
- ✅ 删除 db 重启后种子数据自动重建
- ✅ 补卡审批联动（ApprovalService → AttendanceService）验证通过
