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
import { getQuoteList, submitQuote, confirmQuote } from '@/api/quote'
import type { QuoteListRow, QuoteStatus } from '@/api/quote/types'
import PageHeader from '@/components/app/PageHeader.vue'

const rows = ref<QuoteListRow[]>([])
const total = ref(0)
const loading = ref(false)
const pageNum = ref(1)
const pageSize = 20

const keyword = ref('')
const statusFilter = ref<QuoteStatus | ''>('')

async function loadData() {
  loading.value = true
  try {
    const result = await getQuoteList({
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

const STORAGE_KEY = 'crm-quote-list-cols-v1'
interface ColDef { key: string; label: string; defaultOn: boolean }
const ALL_COL_DEFS: ColDef[] = [
  { key: 'inquiryCode', label: '询盘关联', defaultOn: false },
  { key: 'status',      label: '状态',    defaultOn: true },
  { key: 'totalAmount', label: '金额',    defaultOn: true },
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

const statusLabel: Record<QuoteStatus, string> = {
  draft: '草稿',
  submitted: '已提交',
  confirmed: '已确认',
  rejected: '已驳回',
  expired: '已过期',
}

function statusBadgeClass(status: QuoteStatus): string {
  if (status === 'draft') return 'bg-muted text-muted-foreground'
  if (status === 'submitted') return 'bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]'
  if (status === 'confirmed') return 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
  if (status === 'rejected') return 'bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]'
  return 'bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)]'
}

async function handleSubmit(e: MouseEvent, id: number) {
  e.stopPropagation()
  try { await submitQuote(id); loadData() } catch (err) { console.error(err) }
}

async function handleConfirm(e: MouseEvent, id: number) {
  e.stopPropagation()
  try { await confirmQuote(id); loadData() } catch (err) { console.error(err) }
}

const columnHelper = createColumnHelper<typeof features, QuoteListRow>()
const columns = columnHelper.columns([
  columnHelper.accessor('quoteCode', {
    id: 'quoteCode',
    header: '报价单编码',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue()),
    enableHiding: false,
  }),
  columnHelper.accessor('inquiryCode', {
    id: 'inquiryCode',
    header: '询盘关联',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue() ?? '—'),
  }),
  columnHelper.accessor('customerName', {
    id: 'customerName',
    header: '客户',
    cell: info => h('span', { class: 'text-foreground' }, info.getValue()),
    enableHiding: false,
  }),
  columnHelper.accessor('totalAmount', {
    id: 'totalAmount',
    header: '金额',
    cell: info => {
      const row = info.row.original
      const formatted = new Intl.NumberFormat('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(info.getValue())
      return h('span', { class: 'font-mono-tnum text-right' }, `${row.currency} ${formatted}`)
    },
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
  columnHelper.display({
    id: 'actions',
    header: '操作',
    enableHiding: false,
    cell: ({ row }) => {
      const status = row.original.status
      const btns = []
      if (status === 'draft') {
        btns.push(h('button', {
          class: 'text-[11px] px-2 py-0.5 bg-primary text-primary-foreground rounded cursor-pointer border-0 hover:brightness-105',
          onClick: (e: MouseEvent) => handleSubmit(e, row.original.quoteId),
        }, '提交'))
      }
      if (status === 'submitted') {
        btns.push(h('button', {
          class: 'text-[11px] px-2 py-0.5 bg-[var(--success)] text-white rounded cursor-pointer border-0 hover:brightness-105',
          onClick: (e: MouseEvent) => handleConfirm(e, row.original.quoteId),
        }, '确认'))
      }
      return h('div', { class: 'flex gap-2' }, btns)
    },
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
    <PageHeader title="报价管理" :sub="`共 ${total} 条报价单`" />

    <div class="p-5 space-y-3">
      <!-- Search bar -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] px-3 py-2.5 flex items-center gap-2 flex-wrap">
        <div class="relative">
          <Search :size="13" class="absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground pointer-events-none" />
          <input
            v-model="keyword"
            placeholder="搜索客户或报价..."
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
          <option value="draft">草稿</option>
          <option value="submitted">已提交</option>
          <option value="confirmed">已确认</option>
          <option value="rejected">已驳回</option>
          <option value="expired">已过期</option>
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
              <td :colspan="table.getVisibleFlatColumns().length" class="text-center py-12 text-[13px] text-muted-foreground">暂无报价数据</td>
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
