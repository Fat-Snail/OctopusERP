<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { cn } from '@/lib/utils'

const route = useRoute()
const router = useRouter()

interface NavItem {
  label: string
  path: string
}
interface NavGroup {
  group: string
  items: NavItem[]
}

const groups: NavGroup[] = [
  {
    group: '工作',
    items: [
      { label: '库存看板', path: '/home' },
    ],
  },
  {
    group: '仓库',
    items: [
      { label: '仓库管理', path: '/warehouse' },
      { label: '库存查询', path: '/inventory' },
    ],
  },
  {
    group: '作业',
    items: [
      { label: '入库管理', path: '/inbound' },
      { label: '出库管理', path: '/outbound' },
      { label: '盘点管理', path: '/stocktake' },
    ],
  },
]

function isActive(path: string) {
  return route.path === path || route.path.startsWith(path + '/')
}
</script>

<template>
  <nav class="w-[200px] shrink-0 bg-card border-r border-border flex flex-col">
    <!-- Module header -->
    <div class="px-3.5 pt-3.5 pb-2.5 border-b border-border shrink-0">
      <div class="text-[13px] font-semibold text-foreground">WMS 仓储管理</div>
      <div class="text-[10.5px] text-muted-foreground font-mono tracking-[0.04em] mt-0.5">WMS</div>
    </div>

    <!-- Nav groups -->
    <div class="flex-1 overflow-y-auto py-2 px-2 scrollbar-thin">
      <div v-for="grp in groups" :key="grp.group" class="mb-3">
        <div class="text-[10px] uppercase tracking-[0.06em] font-medium text-muted-foreground px-2 py-1.5">
          {{ grp.group }}
        </div>
        <button
          v-for="item in grp.items"
          :key="item.path"
          :class="cn(
            'w-full flex items-center gap-2 px-2 h-[var(--row-h)] rounded-[var(--radius)] text-[12px] transition-colors cursor-pointer border-0 text-left',
            isActive(item.path)
              ? 'bg-primary-soft text-primary-soft-fg font-medium'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground'
          )"
          @click="router.push(item.path)"
        >
          <span class="flex-1 truncate">{{ item.label }}</span>
        </button>
      </div>
    </div>
  </nav>
</template>
