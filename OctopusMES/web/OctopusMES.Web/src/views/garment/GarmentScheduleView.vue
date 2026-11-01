<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { ChevronLeft, ChevronRight, Calendar, Trash2, Save, Pencil, MousePointerClick, X, CheckCheck, Archive, Lock } from '@lucide/vue'
import {
  getGarmentCalendar,
  getGarmentMonthlySummary,
  deleteGarmentSchedule,
  saveGarmentSchedule,
  batchCreateGarmentSchedule,
  getGarmentArchiveSummary,
} from '@/api/garment'
import type { GarmentCalendar, GarmentScheduleCell, GarmentMonthlySummaryRow, GarmentArchiveSummary } from '@/api/garment/types'
import PageHeader from '@/components/app/PageHeader.vue'

// 默认显示当前月。年度表/归档可单独跳转到历史月份。
const _today = new Date()
const currentYear = ref(_today.getFullYear())
const currentMonth = ref(_today.getMonth() + 1)
// 默认无赛季筛选，避免新月数据被 22春夏 过滤掉
const season = ref('')
const calendar = ref<GarmentCalendar | null>(null)
const monthly = ref<GarmentMonthlySummaryRow[]>([])
const loading = ref(false)

interface CellEditForm {
  customer: string
  orderNo: string
  styleCode: string
  dueDate: string
  plannedQty: number | null
  sewingQty: number | null
  cuttingQty: number | null
  totalHours: number | null
  headcount: number | null
  avgWage: number | null
  fabricStatus: string
  remark: string
}

const selectedCell = ref<{
  team: GarmentCalendar['teams'][number]
  date: string
  cell: GarmentScheduleCell | null
  mode: 'view' | 'edit' | 'create'
  form: CellEditForm
} | null>(null)
const saveError = ref<string | null>(null)
const saving = ref(false)

const monthLabel = computed(() => `${currentYear.value} 年 ${currentMonth.value} 月`)

function startEnd(): { start: string; end: string } {
  const y = currentYear.value
  const m = currentMonth.value
  const start = `${y}-${String(m).padStart(2, '0')}-01`
  const lastDay = new Date(y, m, 0).getDate()
  const end = `${y}-${String(m).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`
  return { start, end }
}

async function loadCalendar() {
  loading.value = true
  try {
    const { start, end } = startEnd()
    calendar.value = await getGarmentCalendar({ startDate: start, endDate: end, season: season.value || undefined })
  } catch (e) {
    console.error(e)
    calendar.value = null
  } finally {
    loading.value = false
  }
}

async function loadMonthly() {
  try {
    const r = await getGarmentMonthlySummary(season.value || undefined)
    monthly.value = r.rows
  } catch {
    monthly.value = []
  }
}

function prevMonth() {
  if (currentMonth.value === 1) { currentYear.value--; currentMonth.value = 12 }
  else currentMonth.value--
  loadCalendar()
}
function nextMonth() {
  if (currentMonth.value === 12) { currentYear.value++; currentMonth.value = 1 }
  else currentMonth.value++
  loadCalendar()
}
function cellKey(teamId: number, date: string) { return `${teamId}_${date}` }

function getCell(teamId: number, date: string): GarmentScheduleCell | null {
  return calendar.value?.cells[cellKey(teamId, date)] ?? null
}

function dayLabel(d: string) {
  const day = new Date(d + 'T00:00:00')
  return day.getDate()
}
function isWeekend(d: string) {
  const day = new Date(d + 'T00:00:00').getDay()
  return day === 0 || day === 6
}

/**
 * 归档判定：日期早于 archiveCutoff 视为归档（只读）。
 * 后端权威，前端只用于 UI 锁定 + 提前阻止编辑。
 */
function isArchivedDate(d: string): boolean {
  const cutoff = calendar.value?.archiveCutoff
  if (!cutoff) return false
  return d < cutoff
}

function jumpToToday() {
  const t = new Date()
  currentYear.value = t.getFullYear()
  currentMonth.value = t.getMonth() + 1
  loadCalendar()
}

function emptyForm(): CellEditForm {
  return {
    customer: '', orderNo: '', styleCode: '', dueDate: '',
    plannedQty: null, sewingQty: null, cuttingQty: null,
    totalHours: null, headcount: null, avgWage: null,
    fabricStatus: '', remark: '',
  }
}
function cellToForm(c: GarmentScheduleCell): CellEditForm {
  return {
    customer: c.customer ?? '',
    orderNo: c.orderNo ?? '',
    styleCode: c.styleCode ?? '',
    dueDate: c.dueDate ?? '',
    plannedQty: c.plannedQty,
    sewingQty: c.sewingQty,
    cuttingQty: c.cuttingQty,
    totalHours: c.totalHours,
    headcount: c.headcount,
    avgWage: c.avgWage,
    fabricStatus: c.fabricStatus ?? '',
    remark: c.remark ?? '',
  }
}

function openCell(team: GarmentCalendar['teams'][number], date: string) {
  saveError.value = null
  const cell = getCell(team.teamId, date)
  if (cell) {
    selectedCell.value = { team, date, cell, mode: 'view', form: cellToForm(cell) }
  } else if (isArchivedDate(date)) {
    // 归档日期且无记录：禁止新建
    return
  } else {
    selectedCell.value = { team, date, cell: null, mode: 'create', form: emptyForm() }
  }
}
function enterEditMode() {
  if (!selectedCell.value) return
  // 归档记录禁止进入编辑模式
  if (selectedCell.value.cell?.archived || isArchivedDate(selectedCell.value.date)) {
    saveError.value = '该日期已归档，不可编辑'
    return
  }
  selectedCell.value.mode = 'edit'
  saveError.value = null
}
function cancelEdit() {
  if (!selectedCell.value) return
  if (selectedCell.value.cell) {
    selectedCell.value.form = cellToForm(selectedCell.value.cell)
    selectedCell.value.mode = 'view'
  } else {
    closeDrawer()
  }
}
async function saveCell() {
  if (!selectedCell.value) return
  saving.value = true
  saveError.value = null
  try {
    const f = selectedCell.value.form
    const payload: Record<string, unknown> = {
      teamId: selectedCell.value.team.teamId,
      scheduleDate: selectedCell.value.date,
      season: season.value || '22春夏',
      customer: f.customer || null,
      orderNo: f.orderNo || null,
      styleCode: f.styleCode || null,
      dueDate: f.dueDate || null,
      plannedQty: f.plannedQty,
      sewingQty: f.sewingQty,
      cuttingQty: f.cuttingQty,
      totalHours: f.totalHours,
      headcount: f.headcount,
      avgWage: f.avgWage,
      fabricStatus: f.fabricStatus || null,
      remark: f.remark || null,
    }
    if (selectedCell.value.cell) {
      payload.scheduleId = selectedCell.value.cell.scheduleId
    }
    await saveGarmentSchedule(payload)
    closeDrawer()
    await loadCalendar()
  } catch (e: unknown) {
    saveError.value = (e as Error).message || '保存失败'
  } finally {
    saving.value = false
  }
}

function closeDrawer() { selectedCell.value = null; saveError.value = null }

async function removeCell() {
  if (!selectedCell.value?.cell) return
  if (selectedCell.value.cell.archived || isArchivedDate(selectedCell.value.date)) {
    saveError.value = '该排程已归档，不可删除'
    return
  }
  if (!confirm('确认删除此排程？')) return
  try {
    await deleteGarmentSchedule(selectedCell.value.cell.scheduleId)
    closeDrawer()
    await loadCalendar()
  } catch (e) {
    saveError.value = (e as Error).message || '删除失败'
  }
}

// ── 多选模式（日历多天批量创建排程）────────────────────────────────
//
// 三种选择方式：
//  1) 拖拽：在同一组内按下 → 拖动 → 释放 → 选中区间
//  2) 点击：单击单元格切换选中（多选模式启用时）
//  3) Shift+点击：以最后一次选中为锚点，连续选中至当前格
//
// 多选模式需先点击"多选模式"按钮启用。启用后单击不再打开抽屉。
const multiSelectMode = ref(false)
// Set 存放 "{teamId}_{yyyy-MM-dd}" 字符串，用 Set 简化去重
const selectedCells = ref<Set<string>>(new Set())
const selectionAnchor = ref<{ teamId: number; date: string } | null>(null)
// 拖拽状态
const dragState = ref<{
  teamId: number
  startDate: string
  currentDate: string
  // 拖拽过程中临时高亮的格子，释放后并入 selectedCells
  dragSet: Set<string>
} | null>(null)
// 批量创建侧栏开关
const batchDrawerOpen = ref(false)

interface BatchForm {
  customer: string
  orderNo: string
  styleCode: string
  dueDate: string
  plannedQty: number | null
  fabricStatus: string
  remark: string
}
const batchForm = ref<BatchForm>(emptyBatchForm())
const batchSaving = ref(false)
const batchError = ref<string | null>(null)

function emptyBatchForm(): BatchForm {
  return {
    customer: '',
    orderNo: '',
    styleCode: '',
    dueDate: '',
    plannedQty: null,
    fabricStatus: '',
    remark: '',
  }
}

function toggleMultiSelectMode() {
  multiSelectMode.value = !multiSelectMode.value
  if (!multiSelectMode.value) clearSelection()
}

function clearSelection() {
  selectedCells.value = new Set()
  selectionAnchor.value = null
  dragState.value = null
}

function isCellSelected(teamId: number, date: string): boolean {
  const k = cellKey(teamId, date)
  if (selectedCells.value.has(k)) return true
  if (dragState.value && dragState.value.dragSet.has(k)) return true
  return false
}

// 选中的天数按 teamId 分组（批量提交时一次只能针对一个组）
const selectedByTeam = computed(() => {
  const map = new Map<number, string[]>()
  for (const k of selectedCells.value) {
    const [teamId, date] = k.split('_')
    const tid = Number(teamId)
    const arr = map.get(tid) ?? []
    arr.push(date)
    map.set(tid, arr)
  }
  // 日期升序
  for (const arr of map.values()) arr.sort()
  return map
})

const selectionSummary = computed(() => {
  const teams = selectedByTeam.value.size
  const days = selectedCells.value.size
  return { teams, days }
})

// 同一组内拖拽生成连续日期区间
function buildRangeKeys(teamId: number, dateA: string, dateB: string): string[] {
  if (!calendar.value) return []
  const start = dateA <= dateB ? dateA : dateB
  const end = dateA <= dateB ? dateB : dateA
  const keys: string[] = []
  for (const d of calendar.value.dates) {
    if (d >= start && d <= end) keys.push(cellKey(teamId, d))
  }
  return keys
}

function onCellPointerDown(team: GarmentCalendar['teams'][number], date: string, ev: PointerEvent) {
  if (!multiSelectMode.value) return
  // 仅响应主键
  if (ev.button !== 0) return
  // 归档日期不可参与多选（它们是只读的）
  if (isArchivedDate(date)) return

  // Shift+点击：以 anchor 为起点，区间选择
  if (ev.shiftKey && selectionAnchor.value && selectionAnchor.value.teamId === team.teamId) {
    const keys = buildRangeKeys(team.teamId, selectionAnchor.value.date, date)
    const newSet = new Set(selectedCells.value)
    keys.forEach(k => newSet.add(k))
    selectedCells.value = newSet
    return
  }

  // 普通拖拽起点
  dragState.value = {
    teamId: team.teamId,
    startDate: date,
    currentDate: date,
    dragSet: new Set(buildRangeKeys(team.teamId, date, date)),
  }
  ;(ev.currentTarget as Element).setPointerCapture?.(ev.pointerId)
}

function onCellPointerEnter(team: GarmentCalendar['teams'][number], date: string) {
  if (!dragState.value) return
  // 限制只能在同一组横向拖动
  if (team.teamId !== dragState.value.teamId) return
  dragState.value.currentDate = date
  dragState.value.dragSet = new Set(buildRangeKeys(team.teamId, dragState.value.startDate, date))
}

function onCellPointerUp() {
  if (!dragState.value) return
  // 把拖拽集合并入正式选中集合
  const next = new Set(selectedCells.value)
  for (const k of dragState.value.dragSet) next.add(k)
  selectedCells.value = next
  selectionAnchor.value = { teamId: dragState.value.teamId, date: dragState.value.currentDate }
  dragState.value = null
}

function onCellClick(team: GarmentCalendar['teams'][number], date: string, ev: MouseEvent) {
  if (!multiSelectMode.value) {
    // 默认行为：打开抽屉（归档日期会进入只读视图）
    openCell(team, date)
    return
  }
  // 多选模式下：点击切换选中（拖拽已经处理过的不需要再切）
  if (ev.shiftKey) return
  if (lastDragMoved.value) {
    lastDragMoved.value = false
    return
  }
  // 归档日期不可参与多选
  if (isArchivedDate(date)) return
  const k = cellKey(team.teamId, date)
  const next = new Set(selectedCells.value)
  if (next.has(k)) next.delete(k)
  else next.add(k)
  selectedCells.value = next
  selectionAnchor.value = { teamId: team.teamId, date }
}

const lastDragMoved = ref(false)
function onCellPointerUpWithFlag() {
  if (dragState.value) {
    if (dragState.value.startDate !== dragState.value.currentDate) lastDragMoved.value = true
  }
  onCellPointerUp()
}

function openBatchDrawer() {
  if (selectedCells.value.size === 0) return
  if (selectedByTeam.value.size > 1) {
    alert('批量创建仅支持单个组的多个日期。请仅选择同一组的格子。')
    return
  }
  batchForm.value = emptyBatchForm()
  batchError.value = null
  batchDrawerOpen.value = true
}

async function submitBatchCreate() {
  if (selectedCells.value.size === 0) return
  if (selectedByTeam.value.size !== 1) {
    batchError.value = '只能选择单个组'
    return
  }
  batchSaving.value = true
  batchError.value = null
  try {
    const [teamId, dates] = [...selectedByTeam.value.entries()][0]
    const f = batchForm.value
    const result = await batchCreateGarmentSchedule({
      teamId,
      dates,
      season: season.value || '22春夏',
      customer: f.customer || null,
      orderNo: f.orderNo || null,
      styleCode: f.styleCode || null,
      dueDate: f.dueDate || null,
      plannedQty: f.plannedQty,
      fabricStatus: f.fabricStatus || null,
      remark: f.remark || null,
    })
    batchDrawerOpen.value = false
    clearSelection()
    multiSelectMode.value = false
    await loadCalendar()
    // 用一个非阻塞 toast 提示
    setTimeout(() => alert(`批量保存成功：新增 ${result.created} 条，更新 ${result.updated} 条`), 50)
  } catch (e: unknown) {
    batchError.value = (e as Error).message || '保存失败'
  } finally {
    batchSaving.value = false
  }
}

function onKeyDown(ev: KeyboardEvent) {
  if (ev.key === 'Escape') {
    if (archiveDrawerOpen.value) archiveDrawerOpen.value = false
    else if (batchDrawerOpen.value) batchDrawerOpen.value = false
    else if (multiSelectMode.value) clearSelection()
  }
}

// ── 归档/年度表 ──────────────────────────────────────────
const archiveDrawerOpen = ref(false)
const archiveSummary = ref<GarmentArchiveSummary | null>(null)
const archiveLoading = ref(false)

async function openArchiveDrawer() {
  archiveDrawerOpen.value = true
  if (archiveSummary.value) return
  archiveLoading.value = true
  try {
    archiveSummary.value = await getGarmentArchiveSummary()
  } catch (e) {
    console.error(e)
  } finally {
    archiveLoading.value = false
  }
}

function jumpToArchiveMonth(year: number, month: number) {
  currentYear.value = year
  currentMonth.value = month
  // 归档查看时通常清空赛季筛选
  season.value = ''
  archiveDrawerOpen.value = false
  loadCalendar()
}

const monthNames = ['1月','2月','3月','4月','5月','6月','7月','8月','9月','10月','11月','12月']

// 当前查看月份是否完全在归档区间（用于显示只读横幅）
const currentMonthIsArchived = computed(() => {
  if (!calendar.value?.archiveCutoff) return false
  const lastDay = calendar.value.dates[calendar.value.dates.length - 1]
  if (!lastDay) return false
  return lastDay < calendar.value.archiveCutoff
})

onMounted(() => {
  window.addEventListener('keydown', onKeyDown)
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeyDown)
})

function fabricBadgeClass(status: string | null) {
  if (!status) return 'bg-muted text-muted-foreground'
  if (status.includes('ok')) return 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
  if (status.includes('未到')) return 'bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]'
  if (status.includes('存仓')) return 'bg-primary-soft text-[var(--primary-soft-fg)]'
  return 'bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]'
}

onMounted(async () => {
  await loadCalendar()
  await loadMonthly()
})
</script>

<template>
  <PageHeader title="服装生产排程" :sub="`${monthLabel}${season ? ` · ${season}` : ''}`">
    <template #actions>
      <button
        class="inline-flex items-center gap-1.5 px-3 h-8 rounded-[var(--radius)] text-[12px] border border-border bg-card hover:bg-muted transition-colors cursor-pointer"
        @click="jumpToToday"
      >
        <Calendar class="size-3.5" /> 今天
      </button>
      <button
        class="inline-flex items-center gap-1.5 px-3 h-8 rounded-[var(--radius)] text-[12px] border border-border bg-card hover:bg-muted transition-colors cursor-pointer"
        @click="openArchiveDrawer"
        title="查看一年前的归档数据（只读）"
      >
        <Archive class="size-3.5" /> 年度归档
      </button>
    </template>
  </PageHeader>

  <div class="p-5 space-y-3">
    <!-- 月份切换 + 赛季 -->
    <div class="bg-card border border-border rounded-[var(--radius)] px-3 py-2.5 flex items-center gap-2 flex-wrap">
      <button
        class="size-7 inline-flex items-center justify-center rounded-[var(--radius)] border border-border hover:bg-muted transition-colors cursor-pointer"
        @click="prevMonth"
      ><ChevronLeft class="size-3.5" /></button>
      <div class="px-3 text-[13px] font-medium min-w-[120px] text-center">{{ monthLabel }}</div>
      <button
        class="size-7 inline-flex items-center justify-center rounded-[var(--radius)] border border-border hover:bg-muted transition-colors cursor-pointer"
        @click="nextMonth"
      ><ChevronRight class="size-3.5" /></button>

      <select
        v-model="season"
        @change="() => { loadCalendar(); loadMonthly() }"
        class="ml-2 h-8 px-2 rounded-[var(--radius)] border border-border bg-card text-[12px] cursor-pointer"
      >
        <option value="">全部赛季</option>
        <option value="22春夏">22春夏</option>
      </select>

      <!-- 多选模式 -->
      <button
        :class="[
          'ml-2 inline-flex items-center gap-1.5 px-3 h-8 rounded-[var(--radius)] text-[12px] border transition-colors cursor-pointer',
          multiSelectMode
            ? 'bg-primary text-primary-foreground border-primary'
            : 'bg-card border-border hover:bg-muted',
        ]"
        @click="toggleMultiSelectMode"
        title="启用后，可在日历上拖拽 / 点选多个日期，批量创建排程"
      >
        <MousePointerClick class="size-3.5" />
        {{ multiSelectMode ? '退出多选' : '多选模式' }}
      </button>

      <!-- 选中状态条 -->
      <div
        v-if="multiSelectMode"
        class="inline-flex items-center gap-2 h-8 px-3 rounded-[var(--radius)] bg-primary-soft text-[var(--primary-soft-fg)] text-[12px]"
      >
        <span>已选 <b>{{ selectionSummary.days }}</b> 天 / {{ selectionSummary.teams }} 组</span>
        <button
          v-if="selectedCells.size > 0"
          class="inline-flex items-center justify-center size-5 rounded hover:bg-white/40 cursor-pointer"
          @click="clearSelection"
          title="清除选中（Esc）"
        ><X class="size-3" /></button>
      </div>

      <button
        v-if="multiSelectMode && selectedCells.size > 0"
        class="inline-flex items-center gap-1.5 px-3 h-8 rounded-[var(--radius)] text-[12px] bg-primary text-primary-foreground hover:opacity-90 transition-opacity cursor-pointer"
        @click="openBatchDrawer"
      >
        <CheckCheck class="size-3.5" /> 批量创建排程
      </button>

      <div class="ml-auto text-[12px] text-muted-foreground">
        共 {{ calendar?.teams.length ?? 0 }} 个组 ×
        {{ calendar?.dates.length ?? 0 }} 天
      </div>
    </div>

    <!-- 多选模式提示 -->
    <div
      v-if="multiSelectMode"
      class="bg-[color-mix(in_oklch,var(--info)_10%,transparent)] border border-[color-mix(in_oklch,var(--info)_30%,transparent)] rounded-[var(--radius)] px-3 py-2 text-[11.5px] text-[var(--info)] flex items-center gap-2"
    >
      <MousePointerClick class="size-3.5 shrink-0" />
      <span>
        <b>多选模式</b>：在同一行（同一个组）按住鼠标拖拽多天 · 点击单元格切换选中 · Shift+点击 选连续区间 · Esc 清除
      </span>
    </div>

    <!-- 归档月份只读横幅 -->
    <div
      v-if="currentMonthIsArchived"
      class="bg-[color-mix(in_oklch,var(--warning)_10%,transparent)] border border-[color-mix(in_oklch,var(--warning)_30%,transparent)] rounded-[var(--radius)] px-3 py-2 text-[11.5px] text-[var(--warning)] flex items-center gap-2"
    >
      <Lock class="size-3.5 shrink-0" />
      <span>
        <b>归档月份</b>：早于 {{ calendar?.archiveCutoff }} 的数据为只读，不可创建/修改/删除
      </span>
    </div>

    <!-- 排程矩阵 -->
    <div class="bg-card border border-border rounded-[var(--radius)] overflow-auto">
      <div v-if="loading" class="p-8 text-center text-muted-foreground text-[13px]">加载中…</div>
      <div v-else-if="!calendar || calendar.teams.length === 0" class="p-8 text-center text-muted-foreground text-[13px]">
        暂无排程数据
      </div>
      <table v-else class="text-[11.5px] w-max border-collapse">
        <thead>
          <tr class="bg-muted/50">
            <th class="sticky left-0 z-10 bg-muted/50 border-r border-border px-2 py-2 text-left font-medium min-w-[140px]">
              组别 / 组长
            </th>
            <th
              v-for="d in calendar.dates"
              :key="d"
              :class="[
                'border-r border-border px-1.5 py-1 text-center font-normal min-w-[78px]',
                isWeekend(d) ? 'bg-[color-mix(in_oklch,var(--warning)_8%,transparent)]' : '',
              ]"
            >
              <div class="text-foreground font-medium">{{ dayLabel(d) }}</div>
              <div class="text-[9.5px] text-muted-foreground">
                {{ ['日','一','二','三','四','五','六'][new Date(d + 'T00:00:00').getDay()] }}
              </div>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="team in calendar.teams" :key="team.teamId" class="border-t border-border hover:bg-muted/30">
            <td class="sticky left-0 z-10 bg-card border-r border-border px-2 py-1.5">
              <div class="font-medium flex items-center gap-1">
                {{ team.groupLabel }}
                <span v-if="team.isOutsource" class="text-[9px] px-1 py-px rounded bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]">外协</span>
              </div>
              <div class="text-[10px] text-muted-foreground">
                {{ team.isOutsource ? team.outsourcePartnerName : team.leader }}
                <span v-if="team.leaderUmcUserId" class="text-[9px] text-[var(--success)] ml-1">UMC#{{ team.leaderUmcUserId }}</span>
              </div>
            </td>
            <td
              v-for="d in calendar.dates"
              :key="d"
              :class="[
                'border-r border-border px-1 py-1 align-top transition-colors select-none relative',
                isArchivedDate(d) ? 'cursor-default' : (multiSelectMode ? 'cursor-crosshair' : 'cursor-pointer'),
                !multiSelectMode && !isArchivedDate(d) && 'hover:bg-primary-soft/40',
                isArchivedDate(d) ? 'bg-muted/40 text-muted-foreground' : '',
                isWeekend(d) && !isCellSelected(team.teamId, d) && !isArchivedDate(d) ? 'bg-[color-mix(in_oklch,var(--warning)_4%,transparent)]' : '',
                isCellSelected(team.teamId, d) ? 'bg-[color-mix(in_oklch,var(--primary)_22%,transparent)] ring-1 ring-inset ring-primary' : '',
              ]"
              :title="isArchivedDate(d) ? `已归档（早于 ${calendar?.archiveCutoff}），只读` : ''"
              @click="onCellClick(team, d, $event)"
              @pointerdown="onCellPointerDown(team, d, $event)"
              @pointerenter="onCellPointerEnter(team, d)"
              @pointerup="onCellPointerUpWithFlag"
            >
              <Lock
                v-if="isArchivedDate(d) && getCell(team.teamId, d)"
                class="absolute top-0.5 right-0.5 size-2.5 text-muted-foreground/60"
              />
              <template v-if="getCell(team.teamId, d) as GarmentScheduleCell | null">
                <div class="space-y-0.5">
                  <div v-if="getCell(team.teamId, d)?.customer || getCell(team.teamId, d)?.orderNo" class="truncate font-medium text-foreground" :title="`${getCell(team.teamId, d)?.customer || ''} ${getCell(team.teamId, d)?.orderNo || ''}`">
                    {{ getCell(team.teamId, d)?.customer }}{{ getCell(team.teamId, d)?.orderNo ? ' ' + getCell(team.teamId, d)?.orderNo : '' }}
                  </div>
                  <div v-if="getCell(team.teamId, d)?.plannedQty" class="text-muted-foreground text-[10px]">
                    預{{ getCell(team.teamId, d)?.plannedQty }}
                  </div>
                  <div v-if="getCell(team.teamId, d)?.sewingQty != null" class="text-[var(--success)] text-[10px]">
                    缝{{ getCell(team.teamId, d)?.sewingQty }}
                  </div>
                  <div v-if="getCell(team.teamId, d)?.cuttingQty != null" class="text-[var(--info)] text-[10px]">
                    裁{{ getCell(team.teamId, d)?.cuttingQty }}
                  </div>
                </div>
              </template>
              <div v-else class="text-muted-foreground/40 text-center">·</div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 月度汇总 -->
    <div class="bg-card border border-border rounded-[var(--radius)] p-3.5">
      <div class="flex items-center justify-between mb-2">
        <div class="text-[13px] font-medium">月度汇总（按组 × 月份）</div>
        <div class="text-[11px] text-muted-foreground">达成率 = 车缝 ÷ 預算</div>
      </div>
      <div class="overflow-auto">
        <table class="text-[12px] w-full">
          <thead>
            <tr class="bg-muted/50 text-left">
              <th class="px-2 py-1.5 font-medium">组别</th>
              <th class="px-2 py-1.5 font-medium">月份</th>
              <th class="px-2 py-1.5 font-medium text-right">預算</th>
              <th class="px-2 py-1.5 font-medium text-right">车缝</th>
              <th class="px-2 py-1.5 font-medium text-right">裁床</th>
              <th class="px-2 py-1.5 font-medium text-right">达成率</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="monthly.length === 0">
              <td colspan="6" class="px-2 py-4 text-center text-muted-foreground">暂无汇总数据</td>
            </tr>
            <tr v-for="row in monthly" :key="`${row.teamId}_${row.yearMonth}`" class="border-t border-border">
              <td class="px-2 py-1.5">{{ row.teamName }}</td>
              <td class="px-2 py-1.5 font-mono">{{ row.yearMonth }}</td>
              <td class="px-2 py-1.5 text-right">{{ row.plannedQty }}</td>
              <td class="px-2 py-1.5 text-right">{{ row.sewingQty }}</td>
              <td class="px-2 py-1.5 text-right">{{ row.cuttingQty }}</td>
              <td class="px-2 py-1.5 text-right">
                <span :class="[
                  'inline-block px-1.5 py-0.5 rounded text-[10.5px]',
                  row.achievementRate >= 90 ? 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
                  : row.achievementRate >= 70 ? 'bg-primary-soft text-[var(--primary-soft-fg)]'
                  : 'bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)]'
                ]">{{ row.achievementRate }}%</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>

  <!-- 单元格抽屉 -->
  <Teleport to="body">
    <div v-if="selectedCell" class="fixed inset-0 z-50 bg-black/40" @click.self="closeDrawer">
      <div class="absolute right-0 top-0 h-full w-[400px] bg-card border-l border-border shadow-lg overflow-y-auto">
        <div class="p-4 border-b border-border flex items-center justify-between">
          <div>
            <div class="text-[14px] font-medium">{{ selectedCell.team.groupLabel }} · {{ selectedCell.team.leader }}</div>
            <div class="text-[12px] text-muted-foreground font-mono">{{ selectedCell.date }}</div>
          </div>
          <button class="text-muted-foreground hover:text-foreground cursor-pointer" @click="closeDrawer">×</button>
        </div>
        <div class="p-4 space-y-3 text-[12px]">
          <!-- View 模式：现有数据只读展示 -->
          <template v-if="selectedCell.mode === 'view' && selectedCell.cell">
            <div class="pb-2 border-b border-border">
              <span :class="[
                'inline-block px-2 py-0.5 rounded text-[10.5px] font-medium',
                selectedCell.cell.scheduleStatus === 'completed' ? 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
                : selectedCell.cell.scheduleStatus === 'in_production' ? 'bg-primary-soft text-[var(--primary-soft-fg)]'
                : selectedCell.cell.scheduleStatus === 'approved' ? 'bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]'
                : selectedCell.cell.scheduleStatus === 'pending_approval' ? 'bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)]'
                : 'bg-muted text-muted-foreground'
              ]">{{ selectedCell.cell.scheduleStatus }}</span>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <div class="text-[10.5px] text-muted-foreground">客户</div>
                <div class="font-medium flex items-center gap-1">
                  {{ selectedCell.cell.customer || '—' }}
                  <span v-if="selectedCell.cell.customerId" class="text-[9px] text-[var(--success)]">CRM#{{ selectedCell.cell.customerId }}</span>
                </div>
              </div>
              <div>
                <div class="text-[10.5px] text-muted-foreground">客户款号</div>
                <div class="font-medium font-mono">
                  {{ selectedCell.cell.orderNo || '—' }}
                  <span v-if="selectedCell.cell.contractId" class="text-[9px] text-[var(--success)] ml-1 font-sans">合同#{{ selectedCell.cell.contractId }}</span>
                </div>
              </div>
              <div class="col-span-2">
                <div class="text-[10.5px] text-muted-foreground">款式编码</div>
                <div class="font-medium font-mono break-all flex items-center gap-1">
                  {{ selectedCell.cell.styleCode || '—' }}
                  <span v-if="selectedCell.cell.plmProductId" class="text-[9px] text-[var(--success)] font-sans">PLM#{{ selectedCell.cell.plmProductId }}</span>
                </div>
              </div>
              <div v-if="selectedCell.cell.oaApprovalId || selectedCell.cell.workOrderId" class="col-span-2 flex gap-3 text-[10.5px]">
                <span v-if="selectedCell.cell.oaApprovalId" class="text-[var(--info)]">OA 审批#{{ selectedCell.cell.oaApprovalId }}</span>
                <span v-if="selectedCell.cell.workOrderId" class="text-[var(--info)]">工单#{{ selectedCell.cell.workOrderId }}</span>
              </div>
              <div>
                <div class="text-[10.5px] text-muted-foreground">货期</div>
                <div class="font-medium font-mono">{{ selectedCell.cell.dueDate || '—' }}</div>
              </div>
              <div>
                <div class="text-[10.5px] text-muted-foreground">回布</div>
                <div>
                  <span :class="['inline-block px-1.5 py-0.5 rounded text-[10.5px]', fabricBadgeClass(selectedCell.cell.fabricStatus)]">
                    {{ selectedCell.cell.fabricStatus || '—' }}
                  </span>
                </div>
              </div>
              <div>
                <div class="text-[10.5px] text-muted-foreground">預算 / 件</div>
                <div class="font-medium">{{ selectedCell.cell.plannedQty ?? '—' }}</div>
              </div>
              <div>
                <div class="text-[10.5px] text-muted-foreground">车缝 / 件</div>
                <div class="font-medium text-[var(--success)]">{{ selectedCell.cell.sewingQty ?? '—' }}</div>
              </div>
              <div>
                <div class="text-[10.5px] text-muted-foreground">裁床 / 件</div>
                <div class="font-medium text-[var(--info)]">{{ selectedCell.cell.cuttingQty ?? '—' }}</div>
              </div>
              <div>
                <div class="text-[10.5px] text-muted-foreground">总工时 / h</div>
                <div class="font-medium">{{ selectedCell.cell.totalHours ?? '—' }}</div>
              </div>
              <div>
                <div class="text-[10.5px] text-muted-foreground">人数</div>
                <div class="font-medium">{{ selectedCell.cell.headcount ?? '—' }}</div>
              </div>
              <div>
                <div class="text-[10.5px] text-muted-foreground">均薪 / 元</div>
                <div class="font-medium">{{ selectedCell.cell.avgWage ?? '—' }}</div>
              </div>
            </div>
            <!-- 归档只读提示 -->
            <div
              v-if="selectedCell.cell.archived || isArchivedDate(selectedCell.date)"
              class="pt-3 border-t border-border flex items-center gap-2 text-[11.5px] text-[var(--warning)] bg-[color-mix(in_oklch,var(--warning)_10%,transparent)] px-2.5 py-2 rounded-[var(--radius)]"
            >
              <Lock class="size-3.5 shrink-0" />
              <span>该排程已归档（早于 {{ calendar?.archiveCutoff }}），只读不可修改</span>
            </div>
            <div v-else class="pt-3 border-t border-border flex gap-2">
              <button
                class="flex-1 inline-flex items-center justify-center gap-1.5 h-8 rounded-[var(--radius)] bg-primary text-primary-foreground hover:opacity-90 transition-opacity cursor-pointer"
                @click="enterEditMode"
              ><Pencil class="size-3.5" /> 编辑</button>
              <button
                class="flex-1 inline-flex items-center justify-center gap-1.5 h-8 rounded-[var(--radius)] border border-border text-[var(--danger)] hover:bg-[color-mix(in_oklch,var(--danger)_8%,transparent)] transition-colors cursor-pointer"
                @click="removeCell"
              >
                <Trash2 class="size-3.5" /> 删除
              </button>
            </div>
          </template>

          <!-- Edit / Create 模式：表单 -->
          <template v-else>
            <div class="pb-2 border-b border-border">
              <span class="inline-block px-2 py-0.5 rounded text-[10.5px] font-medium bg-primary-soft text-[var(--primary-soft-fg)]">
                {{ selectedCell.mode === 'create' ? '新建排程' : '编辑排程' }}
              </span>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="text-[10.5px] text-muted-foreground">客户</label>
                <input v-model="selectedCell.form.customer" placeholder="客户001" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
              </div>
              <div>
                <label class="text-[10.5px] text-muted-foreground">客户款号</label>
                <input v-model="selectedCell.form.orderNo" placeholder="98071" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card font-mono" />
              </div>
              <div class="col-span-2">
                <label class="text-[10.5px] text-muted-foreground">款式编码</label>
                <input v-model="selectedCell.form.styleCode" placeholder="LB5210100龟背绣花" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card font-mono" />
              </div>
              <div>
                <label class="text-[10.5px] text-muted-foreground">货期</label>
                <input v-model="selectedCell.form.dueDate" type="date" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card font-mono" />
              </div>
              <div>
                <label class="text-[10.5px] text-muted-foreground">回布状态</label>
                <input v-model="selectedCell.form.fabricStatus" placeholder="ok / 9-16/ok / 未到" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
              </div>
              <div>
                <label class="text-[10.5px] text-muted-foreground">預算 / 件</label>
                <input v-model.number="selectedCell.form.plannedQty" type="number" min="0" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
              </div>
              <div>
                <label class="text-[10.5px] text-muted-foreground">车缝 / 件</label>
                <input v-model.number="selectedCell.form.sewingQty" type="number" min="0" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
              </div>
              <div>
                <label class="text-[10.5px] text-muted-foreground">裁床 / 件</label>
                <input v-model.number="selectedCell.form.cuttingQty" type="number" min="0" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
              </div>
              <div>
                <label class="text-[10.5px] text-muted-foreground">总工时 / h</label>
                <input v-model.number="selectedCell.form.totalHours" type="number" min="0" step="0.5" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
              </div>
              <div>
                <label class="text-[10.5px] text-muted-foreground">人数</label>
                <input v-model.number="selectedCell.form.headcount" type="number" min="0" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
              </div>
              <div>
                <label class="text-[10.5px] text-muted-foreground">均薪 / 元</label>
                <input v-model.number="selectedCell.form.avgWage" type="number" min="0" step="0.01" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
              </div>
              <div class="col-span-2">
                <label class="text-[10.5px] text-muted-foreground">备注</label>
                <input v-model="selectedCell.form.remark" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
              </div>
            </div>
            <div v-if="saveError" class="text-[11.5px] text-[var(--danger)] bg-[color-mix(in_oklch,var(--danger)_8%,transparent)] px-2 py-1.5 rounded">
              {{ saveError }}
            </div>
            <div class="pt-3 border-t border-border flex gap-2">
              <button
                :disabled="saving"
                class="flex-1 inline-flex items-center justify-center gap-1.5 h-8 rounded-[var(--radius)] bg-primary text-primary-foreground hover:opacity-90 transition-opacity cursor-pointer disabled:opacity-50"
                @click="saveCell"
              ><Save class="size-3.5" /> {{ saving ? '保存中…' : '保存' }}</button>
              <button
                class="flex-1 inline-flex items-center justify-center gap-1.5 h-8 rounded-[var(--radius)] border border-border hover:bg-muted transition-colors cursor-pointer"
                @click="cancelEdit"
              >取消</button>
            </div>
          </template>
        </div>
      </div>
    </div>
  </Teleport>

  <!-- 年度归档浏览抽屉 -->
  <Teleport to="body">
    <div v-if="archiveDrawerOpen" class="fixed inset-0 z-50 bg-black/40" @click.self="archiveDrawerOpen = false">
      <div class="absolute right-0 top-0 h-full w-[460px] bg-card border-l border-border shadow-lg overflow-y-auto">
        <div class="p-4 border-b border-border flex items-center justify-between">
          <div>
            <div class="text-[14px] font-medium flex items-center gap-1.5">
              <Archive class="size-4 text-primary" />
              年度归档
            </div>
            <div class="text-[12px] text-muted-foreground mt-0.5">
              <span v-if="archiveSummary">
                早于 <b class="font-mono text-foreground">{{ archiveSummary.archiveCutoff }}</b> 的数据为归档（只读）
              </span>
              <span v-else>加载中…</span>
            </div>
          </div>
          <button class="text-muted-foreground hover:text-foreground cursor-pointer" @click="archiveDrawerOpen = false">×</button>
        </div>

        <div class="p-4 space-y-3 text-[12px]">
          <div v-if="archiveLoading" class="py-8 text-center text-muted-foreground">加载中…</div>
          <div v-else-if="!archiveSummary || archiveSummary.years.length === 0" class="py-8 text-center text-muted-foreground">
            暂无归档数据
          </div>
          <template v-else>
            <div class="rounded-[var(--radius)] border border-border bg-muted/30 px-3 py-2 flex items-center justify-between">
              <span>共 <b class="font-mono text-foreground">{{ archiveSummary.totalArchived }}</b> 条归档排程</span>
              <span class="text-muted-foreground">{{ archiveSummary.years.length }} 个年度</span>
            </div>

            <div v-for="y in archiveSummary.years" :key="y.year" class="rounded-[var(--radius)] border border-border overflow-hidden">
              <div class="bg-muted/40 px-3 py-2 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="font-medium text-[13px]">{{ y.year }} 年</span>
                  <span
                    v-for="s in y.seasons"
                    :key="s"
                    class="text-[10px] px-1.5 py-0.5 rounded bg-primary-soft text-[var(--primary-soft-fg)]"
                  >{{ s }}</span>
                </div>
                <span class="text-[11px] text-muted-foreground font-mono">{{ y.total }} 条</span>
              </div>
              <div class="p-2 grid grid-cols-4 gap-1.5">
                <button
                  v-for="m in y.months"
                  :key="m.month"
                  class="rounded-[var(--radius)] border border-border bg-card hover:bg-primary-soft hover:border-primary transition-colors px-2 py-2 text-left cursor-pointer"
                  @click="jumpToArchiveMonth(y.year, m.month)"
                >
                  <div class="text-[12px] font-medium flex items-center gap-1">
                    <Lock class="size-2.5 text-muted-foreground" />
                    {{ monthNames[m.month - 1] }}
                  </div>
                  <div class="text-[10.5px] text-muted-foreground font-mono">{{ m.count }} 条</div>
                </button>
              </div>
            </div>

            <div class="pt-3 text-[11px] text-muted-foreground leading-relaxed">
              <p>· 点击月份进入只读视图，所有单元格不可编辑</p>
              <p>· 归档数据由后端校验，强制修改请求会被拒绝（403）</p>
            </div>
          </template>
        </div>
      </div>
    </div>
  </Teleport>

  <!-- 批量创建抽屉 -->
  <Teleport to="body">
    <div v-if="batchDrawerOpen" class="fixed inset-0 z-50 bg-black/40" @click.self="batchDrawerOpen = false">
      <div class="absolute right-0 top-0 h-full w-[440px] bg-card border-l border-border shadow-lg overflow-y-auto">
        <div class="p-4 border-b border-border flex items-center justify-between">
          <div>
            <div class="text-[14px] font-medium flex items-center gap-1.5">
              <CheckCheck class="size-4 text-primary" />
              批量创建排程
            </div>
            <div class="text-[12px] text-muted-foreground mt-0.5">
              将一次性为
              <b class="text-foreground">{{ selectionSummary.days }}</b> 天创建排程
            </div>
          </div>
          <button class="text-muted-foreground hover:text-foreground cursor-pointer" @click="batchDrawerOpen = false">×</button>
        </div>

        <div class="p-4 space-y-4 text-[12px]">
          <!-- 选中预览 -->
          <div class="rounded-[var(--radius)] border border-border bg-muted/30 p-2.5 space-y-1.5 max-h-[180px] overflow-auto">
            <div v-for="[teamId, dates] in selectedByTeam" :key="teamId" class="space-y-1">
              <div class="text-[11px] text-muted-foreground">
                {{ calendar?.teams.find(t => t.teamId === teamId)?.groupLabel }}
                ·
                {{ calendar?.teams.find(t => t.teamId === teamId)?.leader }}
                ·
                共 {{ dates.length }} 天
              </div>
              <div class="flex flex-wrap gap-1">
                <span
                  v-for="d in dates"
                  :key="d"
                  class="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded bg-primary-soft text-[var(--primary-soft-fg)] text-[10.5px] font-mono"
                >
                  {{ d.slice(5) }}
                  <button
                    class="hover:opacity-70 cursor-pointer"
                    @click="() => { const k = `${teamId}_${d}`; const next = new Set(selectedCells); next.delete(k); selectedCells = next; if (next.size === 0) batchDrawerOpen = false }"
                    title="移除"
                  ><X class="size-2.5" /></button>
                </span>
              </div>
            </div>
          </div>

          <div class="text-[11.5px] text-muted-foreground">
            以下字段将一次性应用到所选所有天数。已存在的 (组×天) 排程会被更新，不存在的会新建。
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="text-[10.5px] text-muted-foreground">客户</label>
              <input v-model="batchForm.customer" placeholder="客户001" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
            </div>
            <div>
              <label class="text-[10.5px] text-muted-foreground">客户款号</label>
              <input v-model="batchForm.orderNo" placeholder="98071" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card font-mono" />
            </div>
            <div class="col-span-2">
              <label class="text-[10.5px] text-muted-foreground">款式编码</label>
              <input v-model="batchForm.styleCode" placeholder="LB5210100龟背绣花" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card font-mono" />
            </div>
            <div>
              <label class="text-[10.5px] text-muted-foreground">货期</label>
              <input v-model="batchForm.dueDate" type="date" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card font-mono" />
            </div>
            <div>
              <label class="text-[10.5px] text-muted-foreground">回布状态</label>
              <input v-model="batchForm.fabricStatus" placeholder="ok / 9-16/ok / 未到" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
            </div>
            <div>
              <label class="text-[10.5px] text-muted-foreground">每日預算 / 件</label>
              <input v-model.number="batchForm.plannedQty" type="number" min="0" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
            </div>
            <div class="col-span-2">
              <label class="text-[10.5px] text-muted-foreground">备注</label>
              <input v-model="batchForm.remark" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
            </div>
          </div>

          <div v-if="batchError" class="text-[11.5px] text-[var(--danger)] bg-[color-mix(in_oklch,var(--danger)_8%,transparent)] px-2 py-1.5 rounded">
            {{ batchError }}
          </div>

          <div class="pt-3 border-t border-border flex gap-2">
            <button
              :disabled="batchSaving || selectedCells.size === 0"
              class="flex-1 inline-flex items-center justify-center gap-1.5 h-9 rounded-[var(--radius)] bg-primary text-primary-foreground hover:opacity-90 transition-opacity cursor-pointer disabled:opacity-50"
              @click="submitBatchCreate"
            ><Save class="size-3.5" /> {{ batchSaving ? '保存中…' : `保存 ${selectionSummary.days} 条排程` }}</button>
            <button
              class="px-4 h-9 rounded-[var(--radius)] border border-border hover:bg-muted transition-colors cursor-pointer"
              @click="batchDrawerOpen = false"
            >取消</button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>
