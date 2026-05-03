<script setup lang="ts">
import Badge from '@/components/ui/badge.vue'

const deptStats = [
  { deptName: '章鱼科技集团', userCount: 1, activeCount: 1, percentage: 100 },
  { deptName: '前端组', userCount: 1, activeCount: 1, percentage: 100 },
  { deptName: '后端组', userCount: 1, activeCount: 1, percentage: 100 },
  { deptName: '市场部', userCount: 1, activeCount: 0, percentage: 0 },
  { deptName: '行政部', userCount: 1, activeCount: 1, percentage: 100 },
]

const roleStats = [
  { roleName: '超级管理员', roleKey: 'admin', userCount: 1, status: 1 },
  { roleName: '普通用户', roleKey: 'common', userCount: 3, status: 1 },
  { roleName: '编辑员', roleKey: 'editor', userCount: 2, status: 1 },
]
</script>

<template>
  <div class="grid grid-cols-2 gap-4">
    <!-- By department -->
    <div class="bg-card border border-border rounded-lg overflow-hidden">
      <div class="px-4 py-3 border-b border-border">
        <span class="text-[13px] font-semibold text-foreground">按部门统计用户数</span>
      </div>
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">部门名称</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">用户数</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">启用</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">占比</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in deptStats"
            :key="row.deptName"
            class="border-b border-border last:border-0 hover:bg-subtle"
          >
            <td class="px-4 py-2.5 text-[13px] text-foreground">{{ row.deptName }}</td>
            <td class="px-4 py-2.5 text-[12px] text-right font-mono-tnum text-foreground">{{ row.userCount }}</td>
            <td class="px-4 py-2.5 text-[12px] text-right font-mono-tnum text-success">{{ row.activeCount }}</td>
            <td class="px-4 py-2.5">
              <div class="flex items-center gap-2">
                <div class="flex-1 bg-muted rounded-full h-1.5 overflow-hidden">
                  <div class="h-full bg-primary rounded-full" :style="{ width: `${row.percentage}%` }" />
                </div>
                <span class="text-[11px] font-mono-tnum text-muted-foreground w-8 text-right">{{ row.percentage }}%</span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- By role -->
    <div class="bg-card border border-border rounded-lg overflow-hidden">
      <div class="px-4 py-3 border-b border-border">
        <span class="text-[13px] font-semibold text-foreground">按角色统计用户数</span>
      </div>
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">角色名称</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">权限字符</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">用户数</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">状态</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in roleStats"
            :key="row.roleName"
            class="border-b border-border last:border-0 hover:bg-subtle"
          >
            <td class="px-4 py-2.5 text-[13px] text-foreground">{{ row.roleName }}</td>
            <td class="px-4 py-2.5 text-[12px] font-mono-tnum text-muted-foreground">{{ row.roleKey }}</td>
            <td class="px-4 py-2.5 text-[12px] text-right font-mono-tnum text-foreground">{{ row.userCount }}</td>
            <td class="px-4 py-2.5">
              <Badge :variant="row.status === 1 ? 'success' : 'danger'">
                {{ row.status === 1 ? '正常' : '停用' }}
              </Badge>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
