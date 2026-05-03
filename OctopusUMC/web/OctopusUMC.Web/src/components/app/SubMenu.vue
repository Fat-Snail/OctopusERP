<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { cn } from '@/lib/utils'
import { useUserStore } from '@/store/modules/user'
import { computed } from 'vue'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const isAdmin = computed(() => userStore.roles.includes('admin'))

interface NavItem {
  label: string
  path: string
}
interface NavGroup {
  group: string
  items: NavItem[]
}

const groups = computed<NavGroup[]>(() => [
  {
    group: '工作台',
    items: [
      { label: '首页', path: '/dashboard/workbench' },
      { label: '数据分析', path: '/dashboard/analysis' },
      { label: '统计报表', path: '/dashboard/statistics' },
    ],
  },
  {
    group: '系统管理',
    items: [
      { label: '用户管理', path: '/system/user' },
      { label: '机构管理', path: '/system/dept' },
      { label: '职位管理', path: '/system/post' },
      { label: '菜单管理', path: '/system/menu' },
      { label: '角色管理', path: '/system/role' },
      { label: '字典管理', path: '/system/dict' },
    ],
  },
  {
    group: '系统监控',
    items: [
      { label: '在线用户', path: '/monitor/online' },
      { label: '服务监控', path: '/monitor/server' },
      { label: '操作日志', path: '/monitor/operlog' },
      { label: '访问日志', path: '/monitor/logininfor' },
    ],
  },
  {
    group: '系统工具',
    items: [
      { label: '公告管理', path: '/tool/notice' },
      { label: '文件管理', path: '/tool/file' },
      { label: '任务调度', path: '/tool/job' },
      { label: '系统配置', path: '/tool/config' },
      { label: '邮件短信', path: '/tool/mail' },
      { label: '接入应用', path: '/tool/client' },
    ],
  },
  {
    group: '个人',
    items: [
      { label: '个人中心', path: '/profile' },
    ],
  },
])

function isActive(path: string) {
  return route.path === path || route.path.startsWith(path + '/')
}
</script>

<template>
  <nav class="w-[200px] shrink-0 bg-card border-r border-border flex flex-col">
    <!-- Module header -->
    <div class="px-3.5 pt-3.5 pb-2.5 border-b border-border shrink-0">
      <div class="text-[13px] font-semibold text-foreground">用户中心</div>
      <div class="text-[10.5px] text-muted-foreground font-mono tracking-[0.04em] mt-0.5">UMC</div>
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
