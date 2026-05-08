<script setup lang="ts">
import { ref, h, onMounted } from 'vue'
import {
  useVueTable,
  getCoreRowModel,
  createColumnHelper,
  FlexRender,
} from '@tanstack/vue-table'
import { AlertTriangle } from 'lucide-vue-next'
import { getInventoryList } from '@/api/inventory'
import type { InventoryRow } from '@/api/inventory/types'
import PageHeader from '@/components/app/PageHeader.vue'

const rows = ref<InventoryRow[]>([])
const total = ref(0)
const loading = ref(false)

async function loadData() {
  loading.value = true
  try {
    const result = await getInventoryList({ pageNum: 1, pageSize: 50 })
    rows.value = result.rows
    total.value = result.total
  } catch {
    rows.value = []
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

function stockBadgeClass(row: InventoryRow): string {
  if (row.availableQuantity <= row.safetyStock) {
    return 'bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]'
  }
  return 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
}

const columnHelper = createColumnHelper<InventoryRow>()
const columns = [
  columnHelper.accessor('warehouseName', {
    header: '仓库',
    cell: info => h('span', { class: 'text-muted-foreground text-[11px]' }, info.getValue()),
  }),
  columnHelper.accessor('productName', {
    header: '商品',
    cell: info => h('span', { class: 'font-medium text-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('sku', {
    header: 'SKU',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue() ?? '—'),
  }),
  columnHelper.accessor('quantity', {
    header: '库存量',
    cell: info => h('span', { class: 'font-mono-tnum text-right' }, info.getValue()),
  }),
  columnHelper.accessor('reservedQuantity', {
    header: '已占用',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px] text-muted-foreground' }, info.getValue()),
  }),
  columnHelper.accessor('availableQuantity', {
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
    header: '单位',
    cell: info => h('span', { class: 'text-muted-foreground' }, info.getValue() ?? '—'),
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
    <PageHeader title="库存查询" :sub="`共 ${total} 条库存记录`" />

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
