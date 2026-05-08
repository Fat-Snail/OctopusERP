<script setup lang="ts">
import { ref, h, onMounted } from 'vue'
import {
  useVueTable,
  getCoreRowModel,
  createColumnHelper,
  FlexRender,
} from '@tanstack/vue-table'
import { getQuoteList, submitQuote, confirmQuote } from '@/api/quote'
import type { QuoteListRow, QuoteStatus } from '@/api/quote/types'
import PageHeader from '@/components/app/PageHeader.vue'

const rows = ref<QuoteListRow[]>([])
const total = ref(0)
const loading = ref(false)
const pageNum = ref(1)
const pageSize = 20

async function loadData() {
  loading.value = true
  try {
    const result = await getQuoteList({ pageNum: pageNum.value, pageSize })
    rows.value = result.rows
    total.value = result.total
  } catch {
    rows.value = []
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

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

async function handleSubmit(id: number) {
  try {
    await submitQuote(id)
    loadData()
  } catch (e) {
    console.error(e)
  }
}

async function handleConfirm(id: number) {
  try {
    await confirmQuote(id)
    loadData()
  } catch (e) {
    console.error(e)
  }
}

const columnHelper = createColumnHelper<QuoteListRow>()
const columns = [
  columnHelper.accessor('quoteCode', {
    header: '报价单编码',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('inquiryCode', {
    header: '询盘关联',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue() ?? '—'),
  }),
  columnHelper.accessor('customerName', {
    header: '客户',
    cell: info => h('span', { class: 'text-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('totalAmount', {
    header: '金额',
    cell: info => {
      const row = info.row.original
      const formatted = new Intl.NumberFormat('zh-CN', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }).format(info.getValue())
      return h('span', { class: 'font-mono-tnum text-right' }, `${row.currency} ${formatted}`)
    },
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
    cell: ({ row }) => {
      const status = row.original.status
      const btns = []
      if (status === 'draft') {
        btns.push(h('button', {
          class: 'text-[11px] px-2 py-0.5 bg-primary text-primary-foreground rounded cursor-pointer border-0 hover:brightness-105',
          onClick: () => handleSubmit(row.original.quoteId),
        }, '提交'))
      }
      if (status === 'submitted') {
        btns.push(h('button', {
          class: 'text-[11px] px-2 py-0.5 bg-[var(--success)] text-white rounded cursor-pointer border-0 hover:brightness-105',
          onClick: () => handleConfirm(row.original.quoteId),
        }, '确认'))
      }
      return h('div', { class: 'flex gap-2' }, btns)
    },
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
    <PageHeader title="报价管理" :sub="`共 ${total} 条报价单`" />

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
              暂无报价数据
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    </div>
  </div>
</template>
