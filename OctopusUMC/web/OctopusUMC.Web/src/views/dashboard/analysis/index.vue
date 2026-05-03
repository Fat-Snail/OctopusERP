<script setup lang="ts">
const stats = [
  { label: '用户总数', value: 5, colorClass: 'text-primary' },
  { label: '角色数量', value: 3, colorClass: 'text-success' },
  { label: '菜单数量', value: 24, colorClass: 'text-warning' },
  { label: '在线用户', value: 3, colorClass: 'text-danger' },
]

const summaryData = [
  { module: '用户管理', total: 5, active: 4, disabled: 1, percentage: 80 },
  { module: '角色管理', total: 3, active: 3, disabled: 0, percentage: 100 },
  { module: '部门管理', total: 8, active: 8, disabled: 0, percentage: 100 },
  { module: '职位管理', total: 4, active: 4, disabled: 0, percentage: 100 },
  { module: '菜单管理', total: 24, active: 24, disabled: 0, percentage: 100 },
  { module: '字典管理', total: 3, active: 3, disabled: 0, percentage: 100 },
]
</script>

<template>
  <div class="space-y-4">
    <!-- Stats -->
    <div class="grid grid-cols-4 gap-4">
      <div
        v-for="item in stats"
        :key="item.label"
        class="bg-card border border-border rounded-lg p-6 text-center"
      >
        <div :class="['text-[36px] font-bold font-mono-tnum leading-none', item.colorClass]">{{ item.value }}</div>
        <div class="text-[13px] text-muted-foreground mt-2">{{ item.label }}</div>
      </div>
    </div>

    <!-- Summary table -->
    <div class="bg-card border border-border rounded-lg overflow-hidden">
      <div class="px-4 py-3 border-b border-border flex items-center gap-2">
        <span class="text-[13px] font-semibold text-foreground">数据汇总</span>
        <span class="text-[11px] text-muted-foreground bg-muted px-2 py-0.5 rounded-full">图表将在后续版本接入</span>
      </div>
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">模块名称</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">总数量</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">启用数量</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">停用数量</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">启用率</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in summaryData"
            :key="row.module"
            class="border-b border-border last:border-0 hover:bg-subtle"
          >
            <td class="px-4 py-2.5 text-[13px] text-foreground">{{ row.module }}</td>
            <td class="px-4 py-2.5 text-[12px] text-right font-mono-tnum text-foreground">{{ row.total }}</td>
            <td class="px-4 py-2.5 text-[12px] text-right font-mono-tnum text-success">{{ row.active }}</td>
            <td class="px-4 py-2.5 text-[12px] text-right font-mono-tnum text-danger">{{ row.disabled }}</td>
            <td class="px-4 py-2.5">
              <div class="flex items-center gap-2">
                <div class="flex-1 bg-muted rounded-full h-1.5 overflow-hidden">
                  <div
                    class="h-full bg-primary rounded-full transition-all"
                    :style="{ width: `${row.percentage}%` }"
                  />
                </div>
                <span class="text-[11px] font-mono-tnum text-muted-foreground w-9 text-right">{{ row.percentage }}%</span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
