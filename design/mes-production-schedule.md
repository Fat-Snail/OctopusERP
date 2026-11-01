# OctopusMES — 服装生产排程交互设计

> 面向服装行业的生产排程板，对标金蝶云星空·制造排程板、SAP APO PP/DS、聚水潭服装行业版。
> 核心目标：**让排程员像用 Google Calendar 一样直觉地排产**。

---

## 一、业务背景

### 1.1 服装生产排程的特殊性

| 维度 | 特点 |
|---|---|
| 工单结构 | 一张订单 = 多个款号 × 多个颜色 × 多个尺码（SKU 组合） |
| 工序串行 | 裁剪 → 缝制 → 后整理（熨烫/洗水）→ 质检 → 包装，**前道是后道的前置** |
| 排程粒度 | "产线天"为基本单位（如缝制 1 线，5 月 8 日，做夏装短袖 500 件）|
| 产能约束 | 每条产线有日产能上限（按工人数 × 标准节拍计算）|
| 工序衔接 | 缝制开始日 ≥ 裁剪完成日 + 1 天（缓冲），需要前后串接 |
| 插单频繁 | 紧急订单（电商促销/客户加急）需快速重排已有计划 |
| 多色多码 | 同一个款可能拆分到多条线并行（提速）或跨多天连续做（产能不够）|

### 1.2 排程员的高频操作

1. **新增排程**：选产线、选起止日期、选工单/款号、填计划数量
2. **调整时段**：拖动排程块到新日期 / 拉伸延长或缩短
3. **跨产线移动**：把某个排程从缝制 1 线移到缝制 2 线
4. **冲突处理**：插单导致产能溢出，需要重排周边排程
5. **进度核对**：每日实际产量与计划对比，标记落后

---

## 二、主视图：月历 + 产线泳道甘特图（行业标杆布局）

### 2.1 布局

```
┌──────────────────────────────────────────────────────────────────────┐
│ 排程板    < 2026 年 5 月 >    [月] [周] [日]   [+新增排程]  [⚙ 筛选] │  顶栏
├──────┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┤
│      │1 │2 │3 │4 │5 │6 │7 │8 │9 │10│11│12│13│14│15│16│17│18│19│  日期行
│      │五│六│日│一│二│三│四│五│六│日│一│二│三│四│五│六│日│一│二│  星期
├──────┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
│裁剪 1│ ■■夏装裁布 ■■  │      │ ■卫衣裁 │              │     │  排程块
│      │ 800/1500 53%   │      │ 200件   │              │     │
├──────┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
│缝制 1│      │ ■■夏装短袖 ■■■■■■■  │ ■■春装连衣裙 ■■ │  ●溢出│  红色 = 产能溢出
│      │      │ 1500件 70%             │ 800件          │     │
├──────┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
│缝制 2│      │      │ ■■■■■★加急★ ■■■■■  │ ■■夏装短袖 ■│     │  ★ = 加急标识
│      │      │      │ 600件 急单优先              │ 200件   │     │
├──────┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
│后整  │      │      │      │      │      │ ■■春装熨烫 ■  │     │     │
│      │      │      │      │      │      │ 800件          │     │     │
├──────┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
│质检  │      │      │      │      │      │      │      │ ■■■  │     │     │
│      │      │      │      │      │      │      │      │ 全检 │     │     │
├──────┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┤
│ 产能负荷 │ 60% │ 92% │ 105%●│ 88%│ ...                                   │  底栏
└──────────────────────────────────────────────────────────────────────┘
                            ↑ 红色：超过 100%

筛选侧栏（折叠）：产线类型 / 工单状态 / 优先级 / 客户 / 款类型
```

### 2.2 视觉规范

| 元素 | 颜色 | 说明 |
|---|---|---|
| 排程块底色 | `bg-primary-soft` | 默认，按订单可换色（同订单同色） |
| 加急排程 | 左侧 4px 红色竖条 + ★图标 | 优先级 ≥ 4 |
| 产能溢出 | 整列底部红点 + 排程块边框红 | 当日产能 > 100% |
| 已完成 | `bg-success/14% + text-success` + ✓ | Status=completed |
| 已暂停 | 斜纹底纹（CSS gradient） | Status=paused |
| 进度条 | 排程块底部 2px 绿条 | 实际/计划比例 |
| 衔接虚线 | 1px dashed primary | 显示前后工序依赖（鼠标悬停时） |

---

## 三、新增排程的核心交互（多种方式）

### 3.1 ⭐ 主路径：日历拖拽多选（最常用）

```
步骤 1：鼠标悬停产线某天空白格   → 单元格底色变浅蓝高亮
                                  → 鼠标变 [ + ] 光标
步骤 2：按下左键 + 拖动到 N 天后  → 从起始格到当前格区间高亮
                                  → 实时显示 "5月8日 → 5月12日 共 5 天"
                                  → 区间右上角浮动信息卡：
                                    "缝制 1 · 5 天 · 估计可做 7500 件"
步骤 3：释放鼠标                  → 弹出"新增排程"侧边栏（不遮挡日历）
                                  → 自动填入产线 + 起止日期
步骤 4：用户在侧栏选工单/款号/数量 → 实时显示日均产能、预计完工日
步骤 5：[保存]                    → 排程块出现在日历对应位置 + 进入 pending 状态
```

**视觉反馈细节**：
- 拖拽过程中显示当前区间总产能（如"5 天 × 1500 件/天 = 7500 件"）
- 拖拽到已有排程块上 → 提示"该时段已被占用"，禁止释放
- 拖拽到周末 → 默认跳过（按产线工作日历），可选"包含周末"

### 3.2 辅助路径 1：双击单日

- 双击产线某一天空白格 → 直接弹出新增排程侧栏（默认 1 天）
- 适合：单日紧急小批量排程

### 3.3 辅助路径 2：右键菜单

- 右键产线行 → "快速排入..."
- 弹出对话框，选择已有的 pending 工单 → 系统按产能自动算出起止日期 → 一键确认
- 适合：批量规划阶段

### 3.4 辅助路径 3：从工单池拖入（侧栏 → 日历）

```
左侧工单池                           右侧排程板
─────────                            ─────────
□ WO-001 夏装短袖 1500件 急  ─────►  缝制 1 线某日
□ WO-002 春装连衣裙 800件
□ WO-003 卫衣 200件
```

- 拖动工单卡片到日历某产线某天
- 系统按产线日产能自动延展 N 天（如 1500 件 ÷ 1000 件/天 = 2 天）
- 释放后弹出确认弹层（可微调起止日期）

### 3.5 辅助路径 4：顶栏 [+ 新增排程] 按钮

- 完整表单方式，适合需要详细配置的场景（多产线分配、跨工序串接）
- 表单含：工单 → 产线 → 起止日期 → 班次 → 优先级 → 备注

### 3.6 ⌨️ 键盘快捷键

| 快捷键 | 功能 |
|---|---|
| `N` | 新建排程（弹出表单） |
| `←` / `→` | 选中排程块时左右移动 1 天 |
| `Shift + ←/→` | 选中排程块时左右拉伸 1 天 |
| `Ctrl + D` | 复制选中排程到下一天 |
| `Delete` | 删除选中排程 |
| `Ctrl + Z` | 撤销最近操作（支持多步） |
| `T` | 跳转到今天 |
| `M` / `W` / `D` | 切换月/周/日视图 |

---

## 四、新增排程侧栏（详细表单）

### 4.1 表单结构

```
┌─────────────────────────────┐
│ ✕ 新增排程                  │
├─────────────────────────────┤
│ 产线 *                      │
│ [缝制 1 线          ▼]      │
│                              │
│ 起止日期 *                   │
│ [2026-05-08]  →  [2026-05-12]│
│ 5 天 · 跳过周末（共 5 工作日）│  辅助说明
│ [□ 包含周末]                 │
│                              │
│ 工单 *                      │
│ [WO-2026-001 夏装短袖▼]     │
│ ┌─────────────────────────┐ │
│ │ 客户：示例客户1           │ │  工单卡片预览
│ │ 数量：1500 件             │ │
│ │ 交期：2026-05-20          │ │
│ │ 已排：0 件 / 总 1500 件   │ │
│ └─────────────────────────┘ │
│                              │
│ 计划数量 *                   │
│ [1500] 件   [按交期反推]     │  自动计算或手填
│                              │
│ 日均产能（自动）              │
│ 1500 ÷ 5 天 = 300 件/天      │
│ 产线日产能：1000 件 ✓        │  绿色 = OK
│                              │
│ 班次                        │
│ [全天 ▼] (早/中/晚/全天)     │
│                              │
│ 优先级                      │
│ [○ 1 ○ 2 ● 3 ○ 4 ○ 5]      │  Radio
│ □ 标记为加急 ★              │
│                              │
│ 排程颜色（可选）              │
│ ●●●●●●●  (按订单 / 自定义)│
│                              │
│ 备注                        │
│ [可选...]                    │
├─────────────────────────────┤
│ [取消]  [保存草稿]  [立即排程]│  底部按钮
└─────────────────────────────┘
```

### 4.2 表单关键交互

- **起止日期**：可点击修改，弹出日历微调
- **[按交期反推]** 按钮：根据工单交期 - 当前工序耗时，自动建议起止日期
- **日均产能 vs 产线日产能**：实时计算，超出标红 + 提示
- **工单选择**：支持搜索、过滤、按交期排序
- **保存草稿**：状态 = `draft`，日历显示半透明，不占用产能
- **立即排程**：状态 = `pending`，正常显示，参与产能计算

### 4.3 冲突智能提示

```
┌─────────────────────────────┐
│ ⚠️ 检测到 2 个冲突           │
├─────────────────────────────┤
│ 1. 5月10日 产线产能将达到105%│
│    建议：缩短 1 天 / 换产线 │
│    [一键缩短]  [换缝制 2 线] │
│                              │
│ 2. 前道工序未完成            │
│    裁剪计划 5月9日完成        │
│    本排程从 5月8日开始        │
│    建议：起始日改为 5月10日   │
│    [接受建议]                │
└─────────────────────────────┘
```

---

## 五、调整与移动（已有排程的二次操作）

### 5.1 拖动整体（移动时段）

- 鼠标按住排程块中间 → 拖动到新位置
- 显示原位置半透明残影，新位置实时跟随
- 释放后做冲突检测，无冲突直接保存，有冲突弹提示

### 5.2 拖动两端（拉伸天数）

- 鼠标移到排程块左/右边缘 → 光标变 ↔
- 拖动调整起始日或结束日
- 实时显示新的总天数 + 日均产能
- 计划数量保持不变（默认）或按比例调整（可配置）

### 5.3 跨产线拖动

- 拖动排程块到另一条产线
- 自动重新计算日均产能（新产线产能可能不同）
- 提示原产线"释放产能"

### 5.4 多选批量操作

- `Ctrl + 点击` 多选排程块
- 批量操作：移动 / 删除 / 改优先级 / 改颜色

---

## 六、数据模型（MES 后端新增）

### 6.1 数据表

```sql
-- 生产线
CREATE TABLE mes_production_line (
  LineId         BIGINT PK,
  LineCode       VARCHAR(50) UNIQUE,           -- 'CUT-01' / 'SEW-01' / 'QC-01'
  LineName       VARCHAR(100),                  -- '裁剪 1 线'
  LineType       VARCHAR(20),                   -- cutting / sewing / finishing / qc / packing
  DailyCapacity  DECIMAL(10,2),                 -- 日产能（件/天）
  WorkerCount    INT,                           -- 工人数
  WorkCalendar   JSON,                          -- 工作日历（哪些日期不工作）
  Status         VARCHAR(20) DEFAULT 'active', -- active / maintenance / closed
  Color          VARCHAR(20),                   -- 显示颜色
  SortOrder      INT,
  CreatedAt      DATETIME
);

-- 排程主表
CREATE TABLE mes_schedule (
  ScheduleId       BIGINT PK,
  WorkOrderId      BIGINT FK -> mes_work_order,
  LineId           BIGINT FK -> mes_production_line,
  StartDate        DATE,
  EndDate          DATE,
  PlannedQty       DECIMAL(12,2),               -- 计划总量
  DailyPlannedQty  DECIMAL(10,2),               -- 日均（PlannedQty / 工作日数）
  Shift            VARCHAR(20),                  -- morning / afternoon / night / fullday
  Priority         INT,                          -- 1-5
  IsUrgent         BOOLEAN,                      -- 加急星标
  Status           VARCHAR(20),                  -- draft / pending / in_progress / completed / paused / cancelled
  Color            VARCHAR(20),
  SkipWeekends     BOOLEAN DEFAULT TRUE,
  Remark           TEXT,
  CreatedBy        BIGINT,
  CreatedAt        DATETIME,
  UpdatedAt        DATETIME,

  INDEX idx_line_date (LineId, StartDate, EndDate),
  INDEX idx_workorder (WorkOrderId)
);

-- 排程日明细（每天一条，用于精细产能计算 + 实际产量记录）
CREATE TABLE mes_schedule_daily (
  DailyId       BIGINT PK,
  ScheduleId    BIGINT FK -> mes_schedule,
  Date          DATE,
  PlannedQty    DECIMAL(10,2),                  -- 当日计划
  ActualQty     DECIMAL(10,2),                  -- 当日实际（来自报工系统）
  Status        VARCHAR(20),                     -- normal / behind / ahead / missing
  Note          TEXT,

  UNIQUE INDEX uk_schedule_date (ScheduleId, Date)
);

-- 工序前置依赖（前道未完成不能开始后道）
CREATE TABLE mes_schedule_dependency (
  DependencyId  BIGINT PK,
  PredScheduleId BIGINT FK,                     -- 前道排程
  SuccScheduleId BIGINT FK,                     -- 后道排程
  BufferDays    INT DEFAULT 0,                  -- 缓冲天数
  CreatedAt     DATETIME
);
```

### 6.2 派生计算

**产线日负荷**：

```sql
SELECT
  l.LineId,
  l.LineCode,
  d.Date,
  SUM(sd.PlannedQty) AS DailyPlanned,
  l.DailyCapacity,
  SUM(sd.PlannedQty) / l.DailyCapacity AS LoadRatio
FROM mes_production_line l
JOIN mes_schedule s         ON s.LineId = l.LineId
JOIN mes_schedule_daily sd  ON sd.ScheduleId = s.ScheduleId
WHERE sd.Date BETWEEN @from AND @to
GROUP BY l.LineId, sd.Date;
```

`LoadRatio > 1.0` → 产能溢出，前端标红。

---

## 七、API 接口

```
# 排程板视图
GET    /api/schedule/board?from=2026-05-01&to=2026-05-31&lineIds=
       → 返回：lines[], schedules[], dailyLoad[], conflicts[]

# 新增排程
POST   /api/schedule
       Body: {
         workOrderId, lineId, startDate, endDate,
         plannedQty, shift, priority, isUrgent,
         skipWeekends, color, remark
       }
       Response: { scheduleId, dailyBreakdown[], warnings[] }

# 拖动整体
PUT    /api/schedule/{id}/move
       Body: { newLineId?, newStartDate, newEndDate }

# 拉伸调整
PUT    /api/schedule/{id}/resize
       Body: { newStartDate?, newEndDate?, qtyAdjustMode: 'fixed' | 'proportional' }

# 状态变更
PUT    /api/schedule/{id}/status
       Body: { status: 'in_progress' | 'paused' | 'completed' | 'cancelled' }

# 冲突检测（前置）
POST   /api/schedule/check-conflicts
       Body: { lineId, startDate, endDate, plannedQty, excludeScheduleId? }
       Response: {
         capacityOverflows: [{ date, currentLoad, afterLoad }],
         dependencyViolations: [{ predScheduleId, predEndDate, succStartDate }]
       }

# 工序前置依赖
POST   /api/schedule/{id}/dependency
       Body: { predScheduleId, bufferDays }

# 工单待排池
GET    /api/work-order/pending-schedule
       → 返回所有 status=pending 且未完全排程的工单

# 产线列表
GET    /api/production-line?type=&status=
POST   /api/production-line
PUT    /api/production-line/{id}
DELETE /api/production-line/{id}

# 报工回写（来自 PDA / 工人界面）
POST   /api/schedule/{id}/daily-actual
       Body: { date, actualQty }
```

---

## 八、前端实现要点

### 8.1 推荐技术选型

| 组件 | 推荐 | 备注 |
|---|---|---|
| 整体框架 | Vue 3 + TS + shadcn-vue（与 MES 现有一致） | — |
| 甘特图核心 | **vis-timeline** 或 **dhtmlx-gantt** 或自研 | dhtmlx 商业，vis 免费 |
| 拖拽 | `@vueuse/gesture` 或原生 PointerEvent | 自研更灵活 |
| 日期 | `date-fns` | 操作工作日 + 跨日区间 |
| 撤销栈 | 自研 useUndoStack（简单数组） | 不引入 Redux |

### 8.2 自研拖拽核心代码骨架

```typescript
// composables/useScheduleDragSelect.ts
export function useScheduleDragSelect(opts: {
  lines: Ref<Line[]>
  dates: Ref<Date[]>
  onComplete: (selection: { lineId: number; startDate: Date; endDate: Date }) => void
}) {
  const dragStart = ref<{ lineId: number; date: Date } | null>(null)
  const dragCurrent = ref<{ lineId: number; date: Date } | null>(null)

  function onCellPointerDown(lineId: number, date: Date, ev: PointerEvent) {
    dragStart.value = { lineId, date }
    dragCurrent.value = { lineId, date }
    ;(ev.target as Element).setPointerCapture(ev.pointerId)
  }

  function onCellPointerMove(lineId: number, date: Date) {
    if (!dragStart.value) return
    // 限制只能在同一产线行拖动
    if (lineId !== dragStart.value.lineId) return
    dragCurrent.value = { lineId, date }
  }

  function onCellPointerUp() {
    if (!dragStart.value || !dragCurrent.value) return
    const startDate = dragStart.value.date < dragCurrent.value.date
      ? dragStart.value.date
      : dragCurrent.value.date
    const endDate = dragStart.value.date < dragCurrent.value.date
      ? dragCurrent.value.date
      : dragStart.value.date
    opts.onComplete({ lineId: dragStart.value.lineId, startDate, endDate })
    dragStart.value = null
    dragCurrent.value = null
  }

  // 选中区间用于高亮
  const selectedRange = computed(() => {
    if (!dragStart.value || !dragCurrent.value) return null
    return {
      lineId: dragStart.value.lineId,
      from: dragStart.value.date < dragCurrent.value.date ? dragStart.value.date : dragCurrent.value.date,
      to:   dragStart.value.date < dragCurrent.value.date ? dragCurrent.value.date : dragStart.value.date,
    }
  })

  return { onCellPointerDown, onCellPointerMove, onCellPointerUp, selectedRange }
}
```

### 8.3 性能要点

- **虚拟滚动**：产线 ≥ 30 条或日期范围 ≥ 90 天时启用，避免 DOM 过多
- **节流**：拖拽事件 throttle 60fps
- **批量计算**：产线日负荷在后端聚合一次返回，前端不再二次计算
- **乐观更新**：拖动结束立即更新 UI，后端确认后回写；失败回滚

---

## 九、实施步骤（建议 4 个 sprint）

| Sprint | 任务 | 验收 |
|---|---|---|
| S1（2 周） | 后端：3 张表 + 5 个核心 API（board/create/move/resize/check-conflicts）| 后端集成测试 ≥ 8 个 |
| S2（2 周） | 前端：排程板甘特视图 + 排程块渲染 + 产线日负荷底栏 | 静态数据可展示 |
| S3（2 周） | 前端：拖拽多选创建 + 侧栏表单 + 冲突提示 | 端到端创建一条排程跑通 |
| S4（2 周） | 前端：拖动移动/拉伸/跨产线 + 撤销栈 + 快捷键 + 工单池拖入 | 完整交互演示通过 |

---

## 十、参考文献与对标

| 系统 | 对标点 |
|---|---|
| 金蝶云星空·制造排程板 | 整体布局、产线泳道、产能负荷底栏 |
| SAP APO PP/DS Heuristic | 工序前置依赖、自动重排算法 |
| 聚水潭·服装行业版 | 加急星标、急单优先排程逻辑 |
| Asana Timeline | 拖拽创建/拉伸的微交互流畅度 |
| Google Calendar | 拖拽多日选择的核心范式 |
| Linear Cycle View | 撤销栈 + 快捷键体系 |
| Notion Timeline | 排程块视觉风格（圆角 + 进度条 + 标签）|

---

## 十一、未来扩展（v2）

1. **AI 辅助排程**：输入工单池 + 交期 → 自动生成排程方案，排程员审核调整
2. **甘特图导出 PDF**：周/月排程板一键导出，打印到车间张贴
3. **车间大屏模式**：只读排程板 + 实时进度（接 PDA 报工数据）
4. **多版本对比**：A 方案 vs B 方案并排查看，便于评审
5. **What-if 模拟**：插入紧急订单后预览影响，确认后再应用
6. **跨工序串接智能化**：自动按 BOM 工艺路线串接裁剪→缝制→后整理→质检→包装的排程
