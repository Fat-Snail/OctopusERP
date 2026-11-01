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
import { AlertTriangle, Search, X, Settings2 } from '@lucide/vue'
import { getInventoryList } from '@/api/inventory'
import type { InventoryRow } from '@/api/inventory/types'
import PageHeader from '@/components/app/PageHeader.vue'

const rows = ref<InventoryRow[]>([])
const total = ref(0)
const loading = ref(false)
const pageNum = ref(1)
const pageSize = 20

const keyword = ref('')

async function loadData() {
  loading.value = true
  try {
    const result = await getInventoryList({
      pageNum: pageNum.value,
      pageSize,
      keyword: keyword.value || undefined,
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
function reset() { keyword.value = ''; pageNum.value = 1; loadData() }

onMounted(loadData)

const STORAGE_KEY = 'wms-inventory-list-cols-v1'
interface ColDef { key: string; label: string; defaultOn: boolean }
const ALL_COL_DEFS: ColDef[] = [
  { key: 'sku',              label: 'SKU',    defaultOn: true },
  { key: 'quantity',         label: '库存量', defaultOn: true },
  { key: 'reservedQuantity', label: '已占用', defaultOn: false },
  { key: 'availableQuantity',label: '可用量', defaultOn: true },
  { key: 'unit',             label: '单位',   defaultOn: false },
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

function stockBadgeClass(row: InventoryRow): string {
  if (row.availableQuantity <= row.safetyStock) {
    return 'bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]'
  }
  return 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
}

const columnHelper = createColumnHelper<typeof features, InventoryRow>()
const columns = columnHelper.columns([
  columnHelper.accessor('warehouseName', {
    id: 'warehouseName',
    header: '仓库',
    cell: info => h('span', { class: 'text-muted-foreground text-[11px]' }, info.getValue()),
    enableHiding: false,
  }),
  columnHelper.accessor('productName', {
    id: 'productName',
    header: '商品',
    cell: info => h('span', { class: 'font-medium text-foreground' }, info.getValue()),
    enableHiding: false,
  }),
  columnHelper.accessor('sku', {
    id: 'sku',
    header: 'SKU',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue() ?? '—'),
  }),
  columnHelper.accessor('quantity', {
    id: 'quantity',
    header: '库存量',
    cell: info => h('span', { class: 'font-mono-tnum text-right' }, info.getValue()),
  }),
  columnHelper.accessor('reservedQuantity', {
    id: 'reservedQuantity',
    header: '已占用',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('availableQuantity', {
    id: 'availableQuantity',
    header: '可用量',
    cell: ({ row }) => h(
      'span',
      { class: `inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-medium ${stockBadgeClass(row.original)}` },
      [
        row.original.availableQuantity <= row.original.safetyStock ? h(AlertTriangle, { size: 10 }) : null,
        String(row.original.availableQuantity),
      ].filter(Boolean)
    ),
  }),
  columnHelper.accessor('unit', {
    id: 'unit',
    header: '单位',
    cell: info => h('span', { class: 'text-muted-foreground' }, info.getValue() ?? '—'),
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
    <PageHeader title="库存查询" :sub="`共 ${total} 条库存记录`" />

    <div class="p-5 space-y-3">
      <!-- Search bar -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] px-3 py-2.5 flex items-center gap-2 flex-wrap">
        <div class="relative">
          <Search :size="13" class="absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground pointer-events-none" />
          <input
            v-model="keyword"
            placeholder="搜索仓库或商品名称..."
            class="h-[var(--row-h)] pl-7 pr-3 w-[220px] rounded-[var(--radius)] border border-input bg-background text-[12px] text-foreground placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            @keyup.enter="search"
          />
        </div>
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
              <td :colspan="table.getVisibleFlatColumns().length" class="text-center py-12 text-[13px] text-muted-foreground">暂无数据</td>
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
