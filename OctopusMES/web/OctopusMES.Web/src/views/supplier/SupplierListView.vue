<script setup lang="ts">
import { ref, h, onMounted } from 'vue'
import {
  useVueTable,
  getCoreRowModel,
  createColumnHelper,
  FlexRender,
} from '@tanstack/vue-table'
import { Plus, Pencil, Trash2 } from 'lucide-vue-next'
import { getSupplierList, createSupplier, deleteSupplier } from '@/api/supplier'
import type { SupplierListRow, CreateSupplierRequest, SupplierLevel, SupplierStatus } from '@/api/supplier/types'
import PageHeader from '@/components/app/PageHeader.vue'

const rows = ref<SupplierListRow[]>([])
const total = ref(0)
const loading = ref(false)

async function loadData() {
  loading.value = true
  try {
    const result = await getSupplierList({ pageNum: 1, pageSize: 50 })
    rows.value = result.rows
    total.value = result.total
  } catch {
    rows.value = []
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

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

const columnHelper = createColumnHelper<SupplierListRow>()
const columns = [
  columnHelper.accessor('supplierCode', {
    header: '供应商编码',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('supplierName', {
    header: '供应商名称',
    cell: info => h('span', { class: 'font-medium text-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('contactPerson', {
    header: '联系人',
    cell: info => h('span', { class: 'text-muted-foreground' }, info.getValue() ?? '—'),
  }),
  columnHelper.accessor('contactPhone', {
    header: '联系电话',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue() ?? '—'),
  }),
  columnHelper.accessor('level', {
    header: '等级',
    cell: info => h(
      'span',
      { class: `inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium ${levelBadgeClass(info.getValue())}` },
      info.getValue()
    ),
  }),
  columnHelper.accessor('status', {
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
    cell: ({ row }) => h('div', { class: 'flex items-center gap-2' }, [
      h('button', {
        class: 'p-1 rounded text-muted-foreground hover:text-foreground hover:bg-[var(--subtle)] transition-colors cursor-pointer border-0 bg-transparent',
        onClick: () => openEditDialog(row.original),
      }, [h(Pencil, { size: 13 })]),
      h('button', {
        class: 'p-1 rounded text-muted-foreground hover:text-danger hover:bg-[var(--subtle)] transition-colors cursor-pointer border-0 bg-transparent',
        onClick: () => handleDelete(row.original.supplierId),
      }, [h(Trash2, { size: 13 })]),
    ]),
  }),
]

const table = useVueTable({
  get data() { return rows.value },
  columns,
  getCoreRowModel: getCoreRowModel(),
})

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

    <div class="p-5">
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
              <td
                v-for="cell in row.getVisibleCells()"
                :key="cell.id"
                class="px-3 py-2.5 text-[12px] text-foreground"
              >
                <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
              </td>
            </tr>
            <tr v-if="rows.length === 0">
              <td :colspan="columns.length" class="text-center py-12 text-[13px] text-muted-foreground">暂无数据</td>
            </tr>
          </tbody>
        </table>
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
