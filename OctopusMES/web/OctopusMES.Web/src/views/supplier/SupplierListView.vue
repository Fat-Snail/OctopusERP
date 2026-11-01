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
import { Plus, Pencil, Trash2, Search, X, Settings2 } from '@lucide/vue'
import { getSupplierList, createSupplier, deleteSupplier } from '@/api/supplier'
import type { SupplierListRow, CreateSupplierRequest, SupplierLevel, SupplierStatus } from '@/api/supplier/types'
import PageHeader from '@/components/app/PageHeader.vue'

const rows = ref<SupplierListRow[]>([])
const total = ref(0)
const loading = ref(false)
const pageNum = ref(1)
const pageSize = 20

const keyword = ref('')
const levelFilter = ref<SupplierLevel | ''>('')
const statusFilter = ref<SupplierStatus | ''>('')

async function loadData() {
  loading.value = true
  try {
    const result = await getSupplierList({
      pageNum: pageNum.value,
      pageSize,
      keyword: keyword.value || undefined,
      level: levelFilter.value || undefined,
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
function reset() { keyword.value = ''; levelFilter.value = ''; statusFilter.value = ''; pageNum.value = 1; loadData() }

onMounted(loadData)

const STORAGE_KEY = 'mes-supplier-list-cols-v1'
interface ColDef { key: string; label: string; defaultOn: boolean }
const ALL_COL_DEFS: ColDef[] = [
  { key: 'supplierCode',  label: '供应商编码', defaultOn: true },
  { key: 'contactPerson', label: '联系人',     defaultOn: false },
  { key: 'contactPhone',  label: '联系电话',   defaultOn: false },
  { key: 'level',         label: '等级',       defaultOn: true },
  { key: 'status',        label: '状态',       defaultOn: true },
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

function levelBadgeClass(level: SupplierLevel): string {
  if (level === 'A') return 'bg-primary-soft text-[var(--primary-soft-fg)]'
  if (level === 'B') return 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
  return 'bg-muted text-muted-foreground'
}

function statusBadgeClass(status: SupplierStatus): string {
  return status === 'active'
    ? 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
    : 'bg-muted text-muted-foreground'
}

const statusLabel: Record<SupplierStatus, string> = { active: '合作中', inactive: '停用' }

const columnHelper = createColumnHelper<typeof features, SupplierListRow>()
const columns = columnHelper.columns([
  columnHelper.accessor('supplierCode', {
    id: 'supplierCode',
    header: '供应商编码',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('supplierName', {
    id: 'supplierName',
    header: '供应商名称',
    cell: info => h('span', { class: 'font-medium text-foreground' }, info.getValue()),
    enableHiding: false,
  }),
  columnHelper.accessor('contactPerson', {
    id: 'contactPerson',
    header: '联系人',
    cell: info => h('span', { class: 'text-muted-foreground' }, info.getValue() ?? '—'),
  }),
  columnHelper.accessor('contactPhone', {
    id: 'contactPhone',
    header: '联系电话',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue() ?? '—'),
  }),
  columnHelper.accessor('level', {
    id: 'level',
    header: '等级',
    cell: info => h(
      'span',
      { class: `inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium ${levelBadgeClass(info.getValue())}` },
      info.getValue()
    ),
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
    cell: ({ row }) => h('div', { class: 'flex items-center gap-2' }, [
      h('button', {
        class: 'p-1 rounded text-muted-foreground hover:text-foreground hover:bg-[var(--subtle)] transition-colors cursor-pointer border-0 bg-transparent',
        onClick: (e: MouseEvent) => { e.stopPropagation(); openEditDialog(row.original) },
      }, [h(Pencil, { size: 13 })]),
      h('button', {
        class: 'p-1 rounded text-muted-foreground hover:text-danger hover:bg-[var(--subtle)] transition-colors cursor-pointer border-0 bg-transparent',
        onClick: (e: MouseEvent) => { e.stopPropagation(); handleDelete(row.original.supplierId) },
      }, [h(Trash2, { size: 13 })]),
    ]),
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

// ── Dialog ─────────────────────────────────────────────────────────────────────
const dialogOpen = ref(false)
const editingId = ref<number | null>(null)

const form = ref<CreateSupplierRequest>({
  supplierName: '',
  contactPerson: '',
  contactPhone: '',
  level: 'B',
  status: 'active',
  address: '',
  remark: '',
})

function openCreateDialog() {
  editingId.value = null
  form.value = { supplierName: '', contactPerson: '', contactPhone: '', level: 'B', status: 'active', address: '', remark: '' }
  dialogOpen.value = true
}

function openEditDialog(row: SupplierListRow) {
  editingId.value = row.supplierId
  form.value = {
    supplierName: row.supplierName,
    contactPerson: row.contactPerson ?? '',
    contactPhone: row.contactPhone ?? '',
    level: row.level,
    status: row.status,
  }
  dialogOpen.value = true
}

function closeDialog() { dialogOpen.value = false }

async function handleSubmit() {
  if (!form.value.supplierName.trim()) return
  try {
    await createSupplier(form.value)
    closeDialog()
    loadData()
  } catch (e) { console.error(e) }
}

async function handleDelete(id: number) {
  if (!confirm('确认删除该供应商？')) return
  try {
    await deleteSupplier(id)
    loadData()
  } catch (e) { console.error(e) }
}
</script>

<template>
  <div>
    <PageHeader title="供应商管理" :sub="`共 ${total} 个供应商`">
      <template #actions>
        <button
          class="flex items-center gap-1.5 h-8 px-3 bg-primary text-primary-foreground rounded-[var(--radius)] text-[12px] font-medium hover:brightness-105 transition-all cursor-pointer border-0"
          @click="openCreateDialog"
        >
          <Plus :size="13" />
          新建供应商
        </button>
      </template>
    </PageHeader>

    <div class="p-5 space-y-3">
      <!-- Search bar -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] px-3 py-2.5 flex items-center gap-2 flex-wrap">
        <div class="relative">
          <Search :size="13" class="absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground pointer-events-none" />
          <input
            v-model="keyword"
            placeholder="搜索供应商名称..."
            class="h-[var(--row-h)] pl-7 pr-3 w-[200px] rounded-[var(--radius)] border border-input bg-background text-[12px] text-foreground placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            @keyup.enter="search"
          />
        </div>
        <select
          v-model="levelFilter"
          class="h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground focus-visible:outline-none"
          @change="search"
        >
          <option value="">全部等级</option>
          <option value="A">A 级</option>
          <option value="B">B 级</option>
          <option value="C">C 级</option>
        </select>
        <select
          v-model="statusFilter"
          class="h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground focus-visible:outline-none"
          @change="search"
        >
          <option value="">全部状态</option>
          <option value="active">合作中</option>
          <option value="inactive">停用</option>
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

  <Teleport to="body">
    <div v-if="dialogOpen" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="closeDialog">
      <div class="absolute inset-0 bg-black/40" />
      <div class="relative z-10 w-[480px] bg-card border border-border rounded-[var(--radius-lg)] shadow-lg">
        <div class="flex items-center justify-between px-5 py-4 border-b border-border">
          <h3 class="text-[14px] font-semibold text-foreground">{{ editingId ? '编辑供应商' : '新建供应商' }}</h3>
          <button class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-[var(--subtle)] transition-colors cursor-pointer border-0 bg-transparent" @click="closeDialog">✕</button>
        </div>
        <div class="px-5 py-4 space-y-4">
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">供应商名称 <span class="text-danger">*</span></label>
            <input v-model="form.supplierName" type="text" placeholder="请输入供应商名称" class="w-full h-8 px-3 text-[13px] border border-border rounded-[var(--radius)] bg-background text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-primary" />
          </div>
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="block text-[12px] font-medium text-foreground mb-1.5">联系人</label>
              <input v-model="form.contactPerson" type="text" placeholder="联系人姓名" class="w-full h-8 px-3 text-[13px] border border-border rounded-[var(--radius)] bg-background text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-primary" />
            </div>
            <div>
              <label class="block text-[12px] font-medium text-foreground mb-1.5">联系电话</label>
              <input v-model="form.contactPhone" type="text" placeholder="联系电话" class="w-full h-8 px-3 text-[13px] border border-border rounded-[var(--radius)] bg-background text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-primary" />
            </div>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="block text-[12px] font-medium text-foreground mb-1.5">等级</label>
              <select v-model="form.level" class="w-full h-8 px-3 text-[13px] border border-border rounded-[var(--radius)] bg-background text-foreground focus:outline-none focus:ring-1 focus:ring-primary">
                <option value="A">A 级（优质）</option>
                <option value="B">B 级（普通）</option>
                <option value="C">C 级（待评估）</option>
              </select>
            </div>
            <div>
              <label class="block text-[12px] font-medium text-foreground mb-1.5">状态</label>
              <select v-model="form.status" class="w-full h-8 px-3 text-[13px] border border-border rounded-[var(--radius)] bg-background text-foreground focus:outline-none focus:ring-1 focus:ring-primary">
                <option value="active">合作中</option>
                <option value="inactive">停用</option>
              </select>
            </div>
          </div>
        </div>
        <div class="flex items-center justify-end gap-2 px-5 py-4 border-t border-border">
          <button class="h-8 px-4 text-[13px] border border-border rounded-[var(--radius)] text-foreground hover:bg-[var(--subtle)] transition-colors cursor-pointer bg-transparent" @click="closeDialog">取消</button>
          <button class="h-8 px-4 text-[13px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 transition-all cursor-pointer border-0" @click="handleSubmit">{{ editingId ? '保存' : '创建' }}</button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
