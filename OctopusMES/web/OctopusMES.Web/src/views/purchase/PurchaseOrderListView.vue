<script setup lang="ts">
import { ref, h, onMounted } from 'vue'
import {
  useVueTable,
  getCoreRowModel,
  createColumnHelper,
  FlexRender,
} from '@tanstack/vue-table'
import { Plus } from 'lucide-vue-next'
import { getPurchaseList } from '@/api/purchase'
import type { PurchaseListRow, PurchaseStatus } from '@/api/purchase/types'
import PageHeader from '@/components/app/PageHeader.vue'

const rows = ref<PurchaseListRow[]>([])
const total = ref(0)
const loading = ref(false)

async function loadData() {
  loading.value = true
  try {
    const result = await getPurchaseList({ pageNum: 1, pageSize: 50 })
    rows.value = result.rows
    total.value = result.total
  } catch {
    rows.value = []
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

function statusBadgeClass(status: PurchaseStatus): string {
  if (status === 'approved') return 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
  if (status === 'submitted') return 'bg-primary-soft text-[var(--primary-soft-fg)]'
  if (status === 'completed') return 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
  if (status === 'rejected') return 'bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]'
  if (status === 'draft') return 'bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)]'
  return 'bg-muted text-muted-foreground'
}

const statusLabel: Record<PurchaseStatus, string> = {
  draft: '草稿',
  submitted: '待审批',
  approved: '已批准',
  rejected: '已驳回',
  completed: '已完成',
}

const columnHelper = createColumnHelper<PurchaseListRow>()
const columns = [
  columnHelper.accessor('purchaseCode', {
    header: '采购单号',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px]' }, info.getValue()),
  }),
  columnHelper.accessor('supplierName', {
    header: '供应商',
    cell: info => h('span', { class: 'font-medium text-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('status', {
    header: '状态',
    cell: info => h(
      'span',
      { class: `inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium ${statusBadgeClass(info.getValue())}` },
      statusLabel[info.getValue()]
    ),
  }),
  columnHelper.accessor('totalAmount', {
    header: '总金额',
    cell: info => h('span', { class: 'font-mono-tnum text-right' },
      new Intl.NumberFormat('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(info.getValue())
    ),
  }),
  columnHelper.accessor('oaApprovalId', {
    header: 'OA 审批',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue() ? `#${info.getValue()}` : '—'),
  }),
  columnHelper.accessor('expectedDate', {
    header: '预计到货',
    cell: info => h('span', { class: 'text-muted-foreground' }, info.getValue() ? info.getValue()!.slice(0, 10) : '—'),
  }),
  columnHelper.accessor('createdAt', {
    header: '创建时间',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue().slice(0, 10)),
  }),
]

const table = useVueTable({
  get data() { return rows.value },
  columns,
  getCoreRowModel: getCoreRowModel(),
})
</script>

<template>
  <div>
    <PageHeader title="采购订单" :sub="`共 ${total} 条采购单`">
      <template #actions>
        <button
          class="flex items-center gap-1.5 h-8 px-3 bg-primary text-primary-foreground rounded-[var(--radius)] text-[12px] font-medium hover:brightness-105 transition-all cursor-pointer border-0"
        >
          <Plus :size="13" />
          新建采购单
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
</template>
