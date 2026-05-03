<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { cn } from '@/lib/utils'
import { useOaUserStore } from '@/store/modules/oaUser'
import { computed } from 'vue'

const props = defineProps<{
  pendingCount?: number
  unreadNoticeCount?: number
}>()

const route = useRoute()
const router = useRouter()
const oaUserStore = useOaUserStore()
const isAdmin = computed(() => oaUserStore.isOaAdmin)

interface NavItem {
  label: string
  path: string
  badge?: number
  adminOnly?: boolean
}
interface NavGroup {
  group: string
  items: NavItem[]
}

const groups = computed<NavGroup[]>(() => [
  {
    group: '工作',
    items: [
      { label: '首页', path: '/home' },
      { label: '待我审批', path: '/approval/pending', badge: props.pendingCount },
      { label: '我的申请', path: '/approval/mine' },
      { label: '发起申请', path: '/approval/apply' },
    ],
  },
  {
    group: '应用',
    items: [
      { label: '公告中心', path: '/notice/list', badge: props.unreadNoticeCount },
      { label: '通讯录', path: '/contact' },
      { label: '我的考勤', path: '/attendance/mine' },
      { label: '会议室预订', path: '/meeting/calendar' },
      { label: '我的预订', path: '/meeting/mine' },
    ],
  },
  ...(isAdmin.value ? [{
    group: '管理',
    items: [
      { label: '流程模板', path: '/approval/template' },
      { label: '全部审批', path: '/approval/all' },
      { label: '考勤统计', path: '/attendance/stats' },
      { label: '班次管理', path: '/attendance/shift' },
      { label: '考勤规则', path: '/attendance/rule' },
      { label: '会议室管理', path: '/meeting/room' },
      { label: '职员管理', path: '/employee/list' },
      { label: '用户管理', path: '/admin/user' },
    ],
  }] : []),
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
      <div class="text-[13px] font-semibold text-foreground">OA 协同</div>
      <div class="text-[10.5px] text-muted-foreground font-mono tracking-[0.04em] mt-0.5">OA</div>
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
          <span
            v-if="item.badge && item.badge > 0"
            class="bg-primary text-primary-foreground text-[10px] font-semibold px-1.5 py-0 rounded-full leading-[1.6] min-w-[18px] text-center"
          >
            {{ item.badge > 99 ? '99+' : item.badge }}
          </span>
        </button>
      </div>
    </div>
  </nav>
</template>
