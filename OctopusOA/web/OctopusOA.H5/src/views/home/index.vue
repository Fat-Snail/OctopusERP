<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ClipboardList, Bell, CalendarCheck, Users, ChevronRight } from '@lucide/vue'
import { useUserStore } from '@/store/user'
import { getSummary, getTodos } from '@/api/dashboard'
import type { DashboardSummary, TodoItem } from '@/api/dashboard/types'

const router = useRouter()
const userStore = useUserStore()
const summary = ref<DashboardSummary | null>(null)
const todos = ref<TodoItem[]>([])
const loading = ref(true)

const quickLinks = [
  { label: '发起审批', icon: ClipboardList, path: '/approval' },
  { label: '公告通知', icon: Bell, path: '/notice' },
  { label: '会议预订', icon: CalendarCheck, path: '/meeting' },
  { label: '通讯录', icon: Users, path: '/contact' },
]

function tagClass(type: string | null) {
  return {
    success: 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]',
    warning: 'bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)]',
    danger: 'bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]',
    info: 'bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]',
  }[type ?? ''] ?? 'bg-muted text-muted-foreground'
}

function handleTodoClick(item: TodoItem) {
  if (item.type === 'approval') router.push(`/approval/detail/${item.id}`)
  else if (item.type === 'notice') router.push(`/notice/${item.id}`)
  else if (item.type === 'attendance') router.push('/attendance')
  else if (item.type === 'meeting') router.push('/meeting')
}

onMounted(async () => {
  try {
    const [s, t] = await Promise.all([getSummary(), getTodos()])
    summary.value = s
    todos.value = t.rows
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="pb-2">
    <!-- Greeting header -->
    <div class="bg-primary px-5 pt-10 pb-6">
      <p class="text-white/70 text-[13px] mb-1">早上好 👋</p>
      <h2 class="text-white text-[20px] font-semibold">{{ userStore.userName || '同事' }}</h2>
    </div>

    <!-- Stat cards -->
    <div class="bg-primary px-4 pb-4">
      <div class="bg-background rounded-xl p-4 grid grid-cols-2 gap-3 shadow-sm">
        <div class="text-center">
          <p class="text-[26px] font-bold text-danger">{{ loading ? '—' : summary?.pendingApprovals ?? 0 }}</p>
          <p class="text-[12px] text-muted-foreground mt-1">待审批</p>
        </div>
        <div class="text-center">
          <p class="text-[26px] font-bold text-warning">{{ loading ? '—' : summary?.unreadNotices ?? 0 }}</p>
          <p class="text-[12px] text-muted-foreground mt-1">未读公告</p>
        </div>
        <div class="text-center">
          <p class="text-[26px] font-bold text-primary">{{ loading ? '—' : summary?.myApprovalsCount ?? 0 }}</p>
          <p class="text-[12px] text-muted-foreground mt-1">我的申请</p>
        </div>
        <div class="text-center">
          <p class="text-[26px] font-bold text-success">{{ loading ? '—' : summary?.todayMeetings ?? 0 }}</p>
          <p class="text-[12px] text-muted-foreground mt-1">今日会议</p>
        </div>
      </div>
    </div>

    <!-- Quick actions -->
    <div class="mx-4 mt-4">
      <h3 class="text-[13px] font-semibold text-foreground mb-3">快捷入口</h3>
      <div class="grid grid-cols-4 gap-2">
        <button
          v-for="link in quickLinks"
          :key="link.label"
          class="flex flex-col items-center gap-1.5 py-3 bg-background border border-border rounded-xl hover:bg-muted/50 transition-colors cursor-pointer"
          @click="router.push(link.path)"
        >
          <div class="w-10 h-10 rounded-full bg-primary-soft flex items-center justify-center">
            <component :is="link.icon" :size="18" class="text-[var(--primary-soft-fg)]" />
          </div>
          <span class="text-[11px] text-foreground">{{ link.label }}</span>
        </button>
      </div>
    </div>

    <!-- Todos -->
    <div class="mx-4 mt-4">
      <div class="flex items-center justify-between mb-3">
        <h3 class="text-[13px] font-semibold text-foreground">我的待办</h3>
        <button
          class="flex items-center gap-0.5 text-[12px] text-primary border-0 bg-transparent cursor-pointer"
          @click="router.push('/approval')"
        >
          全部 <ChevronRight :size="14" />
        </button>
      </div>

      <div v-if="loading" class="py-8 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!todos.length" class="py-8 text-center text-muted-foreground text-[12px]">暂无待办事项</div>
      <div v-else class="bg-background border border-border rounded-xl divide-y divide-border overflow-hidden">
        <button
          v-for="item in todos.slice(0, 8)"
          :key="`${item.type}-${item.id}`"
          class="w-full flex items-start gap-3 px-4 py-3.5 hover:bg-muted/50 transition-colors border-0 bg-transparent cursor-pointer text-left"
          @click="handleTodoClick(item)"
        >
          <div class="flex-1 min-w-0">
            <p class="text-[13px] font-medium text-foreground truncate">{{ item.title }}</p>
            <p class="text-[11px] text-muted-foreground mt-0.5 truncate">{{ item.subtitle }}</p>
          </div>
          <div class="shrink-0 flex flex-col items-end gap-1">
            <span
              v-if="item.tag"
              :class="['text-[10px] font-medium px-1.5 py-0.5 rounded-full', tagClass(item.tagType)]"
            >{{ item.tag }}</span>
            <span class="text-[11px] text-muted-foreground">{{ item.time }}</span>
          </div>
        </button>
      </div>
    </div>
  </div>
</template>
