<script setup lang="ts">
import { ref, h, onMounted } from 'vue'
import {
  useVueTable,
  getCoreRowModel,
  createColumnHelper,
  FlexRender,
} from '@tanstack/vue-table'
import { getInquiryList } from '@/api/inquiry'
import type { InquiryListRow, InquiryStatus } from '@/api/inquiry/types'
import PageHeader from '@/components/app/PageHeader.vue'

const rows = ref<InquiryListRow[]>([])
const total = ref(0)
const loading = ref(false)
const pageNum = ref(1)
const pageSize = 20

async function loadData() {
  loading.value = true
  try {
    const result = await getInquiryList({ pageNum: pageNum.value, pageSize })
    rows.value = result.rows
    total.value = result.total
  } catch {
    rows.value = []
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

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

const columnHelper = createColumnHelper<InquiryListRow>()
const columns = [
  columnHelper.accessor('inquiryCode', {
    header: '询盘编码',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('title', {
    header: '标题',
    cell: info => h('span', { class: 'font-medium text-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('customerName', {
    header: '客户',
    cell: info => h('span', { class: 'text-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('status', {
    header: '状态',
    cell: info => h(
      'span',
      { class: `inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium ${statusBadgeClass(info.getValue())}` },
      statusLabel[info.getValue()]
    ),
  }),
  columnHelper.accessor('assignedToName', {
    header: '负责人',
    cell: info => h('span', { class: 'text-muted-foreground' }, info.getValue() ?? '—'),
  }),
  columnHelper.accessor('createdAt', {
    header: '创建时间',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue()),
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
    <PageHeader title="询盘管理" :sub="`共 ${total} 条询盘`" />

    <div class="p-5 space-y-4">
    <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
      <div v-if="loading" class="flex items-center justify-center py-16 text-[13px] text-muted-foreground">
        加载中...
      </div>
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
            <td :colspan="columns.length" class="text-center py-12 text-[13px] text-muted-foreground">
              暂无询盘数据
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    </div>
  </div>
</template>
