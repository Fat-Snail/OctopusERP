# OctopusOA — 功能详细规格

> 本文件包含 OA 系统的完整功能规格、API 接口清单和前端页面说明。
> 仅在开发 OA 具体功能时参考；日常开发见根目录 CLAUDE.md 中的 OA 摘要。

---

## OA 本地权限体系（方案二：UMC 管骨架，OA 本地扩展）

**权限来源合并规则**：
```
UMC Token Claims → roles: ["admin", "common"]      ← 全局角色
OA 本地          → oaRoles: ["oa_admin", "oa_user"] ← OA 角色（控制 OA 菜单/功能）
```

| OA 角色 | oaRole | 权限范围 |
|---------|--------|----------|
| OA 管理员 | oa_admin | 全部菜单 + 流程模板管理 + 查看全部审批 |
| 普通员工 | oa_user | 首页 + 我的工作（发起/我的/待审批）+ 个人中心 |
| 部门主管 | oa_manager | oa_user 权限 + 查看本部门全部审批 |

**OA 菜单树**：
```
OA 系统
├── 首页（工作台 + 4 数据卡 + 快捷入口 + 聚合待办）  /home
├── 公告中心                /notice/list
├── 通讯录                  /contact
├── 我的工作
│   ├── 发起申请            /approval/apply
│   ├── 我的申请            /approval/mine
│   └── 待我审批            /approval/pending
├── 考勤
│   ├── 我的考勤（日历+列表） /attendance/mine
│   ├── 考勤统计（oa_admin） /attendance/stats
│   ├── 班次管理（oa_admin） /attendance/shift
│   └── 默认规则（oa_admin） /attendance/rule
├── 会议室
│   ├── 预订日历            /meeting/calendar
│   ├── 我的预订            /meeting/mine
│   └── 会议室管理（oa_admin）/meeting/room
├── 审批管理（oa_admin）
│   ├── 流程模板            /approval/template
│   └── 全部审批            /approval/all
├── 职员管理（oa_admin）    /employee/list
├── 用户管理（oa_admin）    /admin/user
└── 个人中心                /profile
```

**侧边栏权限过滤**：通过 `useOaUserStore().isOaAdmin` 判断

---

## 可定制审批流引擎

**核心架构**：
```
WorkflowTemplate (流程模板)
  └── WorkflowNode[] (审批节点，有序)
        └── 定义每个节点由谁审批

Approval (审批实例 = 一次具体的申请)
  └── ApprovalRecord[] (审批操作记录)
```

**审批人类型**：

| approverType | 含义 | approverValue 示例 |
|---|---|---|
| `role` | 指定 OA 角色 | `oa_manager` |
| `user` | 指定用户 | `2`（UMC UserId） |
| `dept_leader` | 申请人所在部门的主管 | 留空（运行时解析） |
| `self_select` | 申请人自选审批人 | 留空（提交时指定） |

**FormSchema**：每个模板通过 JSON 定义表单字段，前端动态渲染：
```json
{"fields":[{"key":"leaveType","label":"请假类型","type":"select","options":[...],"required":true},...]}
```

**审批状态流转**：
```
draft → pending → (逐节点审批) → approved
                               → rejected（任一节点驳回）
       → cancelled（申请人撤回，仅 pending 时可撤）
```

**种子模板**：
- 请假审批（leave）：直属主管审批 → HR 确认（2 节点）
- 报销审批（expense）：直属主管审批 → 财务审核 → 总经理审批（3 节点）

**数据表**：

| 表 | 说明 |
|---|---|
| `oa_workflow_template` | 流程模板（TemplateId, TemplateName, TemplateCode, FormSchema, Status） |
| `oa_workflow_node` | 审批节点（NodeId, TemplateId, NodeName, NodeOrder, ApproverType, ApproverValue） |
| `oa_approval` | 审批实例（ApprovalId, TemplateId, Title, ApplicantId, CurrentNodeOrder, Status, FormData） |
| `oa_approval_record` | 审批记录（RecordId, ApprovalId, NodeId, ApproverId, Action, Comment） |

---

## OA API 接口清单

**审批模板管理（oa_admin）**：
```
GET    /api/approval/template/list         模板列表
GET    /api/approval/template/{id}         模板详情（含节点）
POST   /api/approval/template              创建模板
PUT    /api/approval/template              修改模板
DELETE /api/approval/template/{id}         删除模板
POST   /api/approval/template/{id}/nodes   设置模板节点（整体替换）
```

**审批操作**：
```
GET    /api/approval/templates             可用模板列表（发起时选择）
POST   /api/approval/submit                提交审批申请
GET    /api/approval/mine                  我的申请列表
GET    /api/approval/pending               待我审批列表
GET    /api/approval/{id}                  审批详情（含流程记录）
PUT    /api/approval/{id}/approve          通过
PUT    /api/approval/{id}/reject           驳回
PUT    /api/approval/{id}/cancel           撤回（仅申请人）
GET    /api/approval/all                   全部审批（oa_admin）
```

**基础接口**：
```
GET    /api/me                             当前用户信息（从 JWT Claims）
POST   /api/users/sync                     接收 UMC 用户同步（HMAC-SHA256 签名）
GET    /api/users/sync                     已同步用户列表
POST   /api/auth/backchannel-logout        接收 UMC 登出通知
```

---

## 职员管理（入职档案）

**核心流程**：HR 创建临时档案 → 发 H5 链接 → 候选人自助填写 → HR 审核 → 确认入职（自动创建 UMC 账号）

**状态流转（五状态）**：
```
temp(临时)          HR 创建档案，系统生成 H5 token
   ↓
pending(待入职)     候选人通过 H5 填写完信息，等待 HR 审核
   ├→ rejected(已拒绝)   HR 拒绝 / 候选人放弃
   ↓
active(在职)        HR 确认入职 → 自动创建 UMC 账号 + OA 同步
   ↓
resigned(离职)      HR 操作离职 → 禁用 UMC 账号
```

**数据表**：

| 表 | 说明 |
|---|---|
| `Employee` | 职员档案主表（HR 填写 + H5 填写 + 系统字段） |
| `EmployeeEducation` | 教育经历（可多条） |
| `EmployeeWorkHistory` | 工作经历（可多条） |
| `EmployeeFamily` | 家庭成员（可多条） |
| `EmployeeEmergencyContact` | 紧急联系人（至少1人） |

**H5 入职人员填写**（6 个模块）：
- 个人信息：证件照、身份证号/正反面、政治面貌、婚姻状况、籍贯、现居住/户籍地址
- 教育经历：学校、学历、专业、起止时间、学位（可多条）
- 工作经历：公司、职位、起止时间、离职原因（可多条）
- 家庭成员：姓名、关系、工作单位、电话（可多条）
- 紧急联系人：姓名、关系、电话（至少1人）
- 银行卡信息：开户银行、账号、支行

**职员管理 API**：
```
POST   /api/employee                    创建临时档案（返回 h5Token）
GET    /api/employee/list               档案列表（按状态/姓名筛选）
GET    /api/employee/{id}               档案详情（含子表）
PUT    /api/employee/{id}               修改 HR 填写部分
PUT    /api/employee/{id}/confirm       确认入职（→ active）
PUT    /api/employee/{id}/reject        拒绝入职（→ rejected）
PUT    /api/employee/{id}/resign        办理离职（→ resigned）
DELETE /api/employee/{id}               删除档案（仅 temp/rejected）
```

**H5 端 API**（无需登录，token 验证）：
```
GET    /api/h5/onboard/{token}          获取 H5 预填数据
POST   /api/h5/onboard/{token}          提交入职信息（→ pending）
```

---

## OA 用户管理

```
GET    /api/oa/user/list                已同步用户列表（含 oaRoles）
PUT    /api/oa/user/{id}/roles          修改用户 OA 角色
GET    /api/oa/role/list                OA 角色列表
```

---

## 审批流程图组件

审批详情页使用 `WorkflowGraph.vue` 纯 CSS 流程图组件替代 el-steps：
```
[申请人] → [直属主管审批 ✓] → [财务审核 ✓] → [总经理审批 ●] → [进行中]
```
节点状态：绿色(已通过)、红色(已驳回)、橙色(审批中)、灰色(未到达)

---

## SSO 登录流程（完整链路）

```
OA 点击登录
  → oidc-client-ts signinRedirect()
  → UMC /connect/authorize（未登录）
  → 重定向到 UMC 前端 http://localhost:5173/login?returnUrl=/connect/authorize?...
  → 用户输入 admin/Admin@123 登录
  → UMC 前端检测 returnUrl 以 /connect/ 开头
  → window.location.href 跳回 UMC API /connect/authorize（已有 Cookie）
  → OpenIddict 颁发授权码
  → 重定向到 OA /callback
  → oidc-client-ts signinRedirectCallback() 换取 access_token
  → 进入 OA 首页
```

---

## OA 常用功能（五步走，已完成）

### 通讯录（基于 UMC 部门同步）

**数据表**：
| 表 | 说明 |
|---|---|
| `OaDept` | 从 UMC 同步的部门缓存（deptId, parentId, deptName, orderNum, status） |
| `OaUserDept` | 用户-部门关联（UmcUserId, DeptId, PostId, PostName, IsPrimary） |

**API**：
```
GET  /api/contact/dept/tree              部门树（含每节点用户数）
GET  /api/contact/users?deptId=&keyword= 用户列表（部门过滤 + 搜索）
GET  /api/contact/user/{umcUserId}       用户详情（含跨公司任职）
POST /api/contact/dept/sync              UMC 部门推送（HMAC 签名）
```

**前端**：左树右卡片 + 详情抽屉（拨号/发邮件/复制）

### 公告通知（UMC 发布 → OA 接收）

**数据表**：
| 表 | 说明 |
|---|---|
| `OaNotice` | 镜像 UMC 公告（含类型 1=通知/2=公告/3=紧急、priority） |
| `OaNoticeRead` | 用户已读记录（每用户独立） |

**API**：
```
POST /api/notice/sync                    接收 UMC 推送（HMAC）
GET  /api/notice/list?type=&status=      列表（含已读状态）
GET  /api/notice/{id}                    详情（自动标记已读）
GET  /api/notice/unread/count            未读数
GET  /api/notice/latest                  首页最新 5 条
```

### 考勤打卡（含日历视图 + 多班次）

**数据表**：
| 表 | 说明 |
|---|---|
| `OaAttendanceRule` | 班次（Code/Name/WorkStartTime/WorkEndTime/LateThresholdMin/EarlyLeaveThresholdMin/IpWhiteList/IsDefault） |
| `OaUserShift` | 用户 → 班次关联（UmcUserId, ShiftId, AssignedAt） |
| `OaAttendance` | 每日打卡（UserId + Date 唯一，含 CheckIn/Out Time/Status/Ip） |
| `OaAttendanceFix` | 补卡记录（关联 ApprovalId） |

**4 种子班次**：标准班（09:00-18:00）/ 早班（07:00-16:00）/ 晚班（14:00-23:00）/ 弹性班（11:00-20:00）

**API**：
```
POST /api/attendance/check-in            上班打卡
POST /api/attendance/check-out           下班打卡
GET  /api/attendance/today               今日状态
GET  /api/attendance/mine?month=         月度考勤
GET  /api/attendance/stats?month=        统计
GET  /api/attendance/abnormal?month=     异常列表
GET  /api/attendance/rule                默认规则
PUT  /api/attendance/rule                修改默认规则
GET  /api/attendance/shift/list          班次列表
POST /api/attendance/shift               新建班次
PUT  /api/attendance/shift               修改
DELETE /api/attendance/shift/{id}        删除（默认/已分配不可删）
PUT  /api/attendance/shift/{id}/default  设为默认
GET  /api/attendance/user-shift/list     所有用户的班次分配
PUT  /api/attendance/user-shift          分配用户班次
```

### 待办中心（聚合首页）

**API**：
```
GET /api/dashboard/summary               首页 4 张卡数据 + 今日考勤快照
GET /api/dashboard/todos?type=           聚合待办（all / approval / notice / attendance / meeting）
```

**前端首页**：欢迎卡 + 4 数据卡 + 5 快捷入口 + 聚合待办列表（按类型切换）

### 会议室预订

**数据表**：
| 表 | 说明 |
|---|---|
| `OaMeetingRoom` | 会议室（Name, Capacity, Location, Equipment[JSON], Status） |
| `OaMeetingBooking` | 预订（RoomId, Title, UmcUserId, StartTime, EndTime, Attendees[JSON], Status: confirmed/cancelled） |

**冲突检测**：同房间 `status=confirmed` 且时段重叠 → 返回 409

**API**：
```
GET/POST/PUT/DELETE /api/meeting/room       会议室 CRUD
GET  /api/meeting/room/{id}/calendar        单房间日历（day/week）
POST /api/meeting/booking                   预订（冲突检测）
PUT  /api/meeting/booking/{id}/cancel       取消（仅预订人 + 未开始）
GET  /api/meeting/booking/mine              我的预订
GET  /api/meeting/booking/today             今日参与（主办+被邀）
GET  /api/meeting/calendar                  总览日历（所有房间）
```

---

## 其他 OA 功能（未来扩展）

| 模块 | 功能描述 |
|---|---|
| **文件上传** | OSS 预签名上传 |
| **即时消息** | 站内信 / @我的消息 |
| **任务协作** | 任务看板 / 分派跟踪 |
| **文档中心** | 知识库 + 版本管理 |
