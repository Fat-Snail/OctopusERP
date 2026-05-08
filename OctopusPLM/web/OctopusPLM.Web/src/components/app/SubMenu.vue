<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { cn } from '@/lib/utils'

const route = useRoute()
const router = useRouter()

interface NavItem {
  label: string
  path: string
  disabled?: boolean
}
interface NavGroup {
  group: string
  items: NavItem[]
}

const groups: NavGroup[] = [
  {
    group: '商品',
    items: [
      { label: '商品列表', path: '/plm/product' },
    ],
  },
  {
    group: '类目',
    items: [
      { label: '类目管理', path: '/plm/category/manage' },
      { label: '类目模型', path: '/plm/category-model' },
    ],
  },
  {
    group: '渠道',
    items: [
      { label: '渠道映射', path: '/plm/channel' },
    ],
  },
  {
    group: '设计',
    items: [
      { label: 'BOM管理', path: '/plm/bom', disabled: true },
    ],
  },
  {
    group: '系统',
    items: [
      { label: '模型管理', path: '/plm/system/model' },
    ],
  },
  {
    group: '个人',
    items: [
      { label: '个人中心', path: '/plm/profile' },
    ],
  },
]

function isActive(path: string) {
  return route.path === path || route.path.startsWith(path + '/')
}

function handleClick(item: NavItem) {
  if (item.disabled) return
  router.push(item.path)
}
</script>

<template>
  <nav class="w-[200px] shrink-0 bg-card border-r border-border flex flex-col">
    <!-- Module header -->
    <div class="px-3.5 pt-3.5 pb-2.5 border-b border-border shrink-0">
      <div class="text-[13px] font-semibold text-foreground">PLM</div>
      <div class="text-[10.5px] text-muted-foreground font-mono tracking-[0.04em] mt-0.5">商品生命周期管理</div>
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
          :disabled="item.disabled"
          :class="cn(
            'w-full flex items-center gap-2 px-2 h-[var(--row-h)] rounded-[var(--radius)] text-[12px] transition-colors cursor-pointer border-0 text-left',
            isActive(item.path)
              ? 'bg-primary-soft text-primary-soft-fg font-medium'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground',
            item.disabled && 'opacity-40 cursor-not-allowed hover:bg-transparent hover:text-muted-foreground'
          )"
          @click="handleClick(item)"
        >
          <span class="flex-1 truncate">{{ item.label }}</span>
        </button>
      </div>
    </div>
  </nav>
</template>
