<script setup lang="ts">
import { ref, computed, h, onMounted, onUnmounted } from 'vue'
import {
  useTable,
  coreFeatures,
  columnVisibilityFeature,
  createColumnHelper,
  FlexRender,
} from '@tanstack/vue-table'
import type { ColumnVisibilityState } from '@tanstack/vue-table'

const features = { ...coreFeatures, columnVisibilityFeature }
import { Search, X, Settings2 } from '@lucide/vue'
import { getInquiryList } from '@/api/inquiry'
import type { InquiryListRow, InquiryStatus } from '@/api/inquiry/types'
import PageHeader from '@/components/app/PageHeader.vue'

const rows = ref<InquiryListRow[]>([])
const total = ref(0)
const loading = ref(false)
const pageNum = ref(1)
const pageSize = 20

const keyword = ref('')
const statusFilter = ref<InquiryStatus | ''>('')

async function loadData() {
  loading.value = true
  try {
    const result = await getInquiryList({
      pageNum: pageNum.value,
      pageSize,
      keyword: keyword.value || undefined,
      status: statusFilter.value || undefined,
    })
    rows.value = result.rows
    total.value = result.total
  } catch {
    rows.value = []
  } finally {
    loading.value = false
  }
}

function search() { pageNum.value = 1; loadData() }
function reset() { keyword.value = ''; statusFilter.value = ''; pageNum.value = 1; loadData() }

onMounted(loadData)

const STORAGE_KEY = 'crm-inquiry-list-cols-v1'
interface ColDef { key: string; label: string; defaultOn: boolean }
const ALL_COL_DEFS: ColDef[] = [
  { key: 'status',         label: '状态',   defaultOn: true },
  { key: 'assignedToName', label: '负责人', defaultOn: true },
  { key: 'createdAt',      label: '创建时间', defaultOn: false },
]

function loadColVis(): ColumnVisibilityState {
  try { const r = localStorage.getItem(STORAGE_KEY); if (r) return JSON.parse(r) } catch { /* */ }
  return Object.fromEntries(ALL_COL_DEFS.map(c => [c.key, c.defaultOn]))
}
const colVisibility = ref<ColumnVisibilityState>(loadColVis())
const colPickerOpen = ref(false)

function toggleCol(key: string) {
  colVisibility.value = { ...colVisibility.value, [key]: !colVisibility.value[key] }
  localStorage.setItem(STORAGE_KEY, JSON.stringify(colVisibility.value))
}
function resetCols() {
  colVisibility.value = Object.fromEntries(ALL_COL_DEFS.map(c => [c.key, c.defaultOn]))
  localStorage.setItem(STORAGE_KEY, JSON.stringify(colVisibility.value))
}
function onDocClick(e: MouseEvent) {
  if (colPickerOpen.value && !(e.target as Element).closest('[data-col-picker]')) colPickerOpen.value = false
}
onMounted(() => document.addEventListener('click', onDocClick))
onUnmounted(() => document.removeEventListener('click', onDocClick))

const statusLabel: Record<InquiryStatus, string> = {
  open: '待处理',
  in_progress: '处理中',
  quoted: '已报价',
  closed: '已关闭',
  cancelled: '已取消',
}

function statusBadgeClass(status: InquiryStatus): string {
  if (status === 'open') return 'bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]'
  if (status === 'in_progress') return 'bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)]'
  if (status === 'quoted') return 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
  if (status === 'closed') return 'bg-muted text-muted-foreground'
  return 'bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]'
}

const columnHelper = createColumnHelper<typeof features, InquiryListRow>()
const columns = columnHelper.columns([
  columnHelper.accessor('inquiryCode', {
    id: 'inquiryCode',
    header: '询盘编码',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue()),
    enableHiding: false,
  }),
  columnHelper.accessor('title', {
    id: 'title',
    header: '标题',
    cell: info => h('span', { class: 'font-medium text-foreground' }, info.getValue()),
    enableHiding: false,
  }),
  columnHelper.accessor('customerName', {
    id: 'customerName',
    header: '客户',
    cell: info => h('span', { class: 'text-foreground' }, info.getValue()),
    enableHiding: false,
  }),
  columnHelper.accessor('status', {
    id: 'status',
    header: '状态',
    cell: info => h(
      'span',
      { class: `inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium ${statusBadgeClass(info.getValue())}` },
      statusLabel[info.getValue()]
    ),
  }),
  columnHelper.accessor('assignedToName', {
    id: 'assignedToName',
    header: '负责人',
    cell: info => h('span', { class: 'text-muted-foreground' }, info.getValue() ?? '—'),
  }),
  columnHelper.accessor('createdAt', {
    id: 'createdAt',
    header: '创建时间',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue().slice(0, 10)),
  }),
])

const table = useTable({
  features,
  get data() { return rows.value },
  columns,
  state: { get columnVisibility() { return colVisibility.value } },
  onColumnVisibilityChange: u => {
    colVisibility.value = typeof u === 'function' ? u(colVisibility.value) : u
    localStorage.setItem(STORAGE_KEY, JSON.stringify(colVisibility.value))
  },
})

const totalPages = computed(() => Math.ceil(total.value / pageSize))
</script>

<template>
  <div>
    <PageHeader title="询盘管理" :sub="`共 ${total} 条询盘`" />

    <div class="p-5 space-y-3">
      <!-- Search bar -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] px-3 py-2.5 flex items-center gap-2 flex-wrap">
        <div class="relative">
          <Search :size="13" class="absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground pointer-events-none" />
          <input
            v-model="keyword"
            placeholder="搜索标题或客户..."
            class="h-[var(--row-h)] pl-7 pr-3 w-[200px] rounded-[var(--radius)] border border-input bg-background text-[12px] text-foreground placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            @keyup.enter="search"
          />
        </div>
        <select
          v-model="statusFilter"
          class="h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground focus-visible:outline-none"
          @change="search"
        >
          <option value="">全部状态</option>
          <option value="open">待处理</option>
          <option value="in_progress">处理中</option>
          <option value="quoted">已报价</option>
          <option value="closed">已关闭</option>
          <option value="cancelled">已取消</option>
        </select>
        <button
          class="h-[var(--row-h)] px-3 rounded-[var(--radius)] border border-input text-[12px] text-foreground hover:bg-subtle transition-colors cursor-pointer bg-transparent"
          @click="search"
        >搜索</button>
        <button
          class="h-[var(--row-h)] px-3 rounded-[var(--radius)] border border-input text-[12px] text-muted-foreground hover:text-foreground hover:bg-subtle transition-colors cursor-pointer bg-transparent flex items-center gap-1"
          @click="reset"
        >
          <X :size="11" />重置
        </button>

        <!-- Column picker -->
        <div class="relative ml-auto" data-col-picker>
          <button
            class="h-[var(--row-h)] px-3 rounded-[var(--radius)] border border-input text-[12px] text-muted-foreground hover:text-foreground hover:bg-subtle transition-colors cursor-pointer bg-transparent flex items-center gap-1.5"
            :class="colPickerOpen ? 'border-primary text-primary' : ''"
            @click.stop="colPickerOpen = !colPickerOpen"
          >
            <Settings2 :size="13" />列设置
          </button>
          <div
            v-if="colPickerOpen"
            class="absolute right-0 top-full mt-1 z-30 w-[160px] bg-card border border-border rounded-[var(--radius-lg)] shadow-lg p-2"
            @click.stop
          >
            <div class="text-[11px] text-muted-foreground px-2 py-1 mb-1">显示列</div>
            <label
              v-for="col in ALL_COL_DEFS"
              :key="col.key"
              class="flex items-center gap-2 px-2 py-1.5 rounded-[var(--radius)] hover:bg-subtle cursor-pointer text-[12px]"
            >
              <input type="checkbox" :checked="!!colVisibility[col.key]" class="w-3.5 h-3.5 accent-primary" @change="toggleCol(col.key)" />
              {{ col.label }}
            </label>
            <div class="border-t border-border mt-1 pt-1">
              <button class="w-full text-left px-2 py-1 text-[11px] text-muted-foreground hover:text-foreground cursor-pointer border-0 bg-transparent" @click="resetCols">恢复默认</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Table -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <div v-if="loading" class="flex items-center justify-center py-16 text-[13px] text-muted-foreground">加载中...</div>
        <table v-else class="w-full border-collapse">
          <thead>
            <tr v-for="hg in table.getHeaderGroups()" :key="hg.id">
              <th
                v-for="header in hg.headers"
                :key="header.id"
                class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-[var(--subtle)] px-3 py-2.5 font-medium border-b border-border"
              >
                <FlexRender :render="header.column.columnDef.header" :props="header.getContext()" />
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="row in table.getRowModel().rows"
              :key="row.id"
              class="border-b border-border last:border-0 hover:bg-[var(--subtle)] transition-colors"
            >
              <td v-for="cell in row.getVisibleCells()" :key="cell.id" class="px-3 py-2.5 text-[12px] text-foreground">
                <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
              </td>
            </tr>
            <tr v-if="rows.length === 0">
              <td :colspan="table.getVisibleFlatColumns().length" class="text-center py-12 text-[13px] text-muted-foreground">暂无询盘数据</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div class="flex items-center justify-end text-[12px] text-muted-foreground gap-1">
        <button
          class="h-7 px-3 rounded-[var(--radius)] border border-input text-[12px] hover:bg-subtle transition-colors cursor-pointer bg-transparent disabled:opacity-40 disabled:cursor-not-allowed"
          :disabled="pageNum <= 1"
          @click="pageNum > 1 && (pageNum--, loadData())"
        >上一页</button>
        <span class="px-2 tabular-nums">{{ pageNum }} / {{ totalPages || 1 }}</span>
        <button
          class="h-7 px-3 rounded-[var(--radius)] border border-input text-[12px] hover:bg-subtle transition-colors cursor-pointer bg-transparent disabled:opacity-40 disabled:cursor-not-allowed"
          :disabled="pageNum >= totalPages"
          @click="pageNum < totalPages && (pageNum++, loadData())"
        >下一页</button>
      </div>
    </div>
  </div>
</template>
