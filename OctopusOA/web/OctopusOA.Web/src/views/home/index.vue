<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  ClipboardList, Bell, Clock, CalendarDays,
  Plus, Megaphone, Users, BookOpen, ArrowRight
} from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Card from '@/components/ui/card.vue'
import CardHeader from '@/components/ui/card-header.vue'
import CardContent from '@/components/ui/card-content.vue'
import Badge from '@/components/ui/badge.vue'
import Avatar from '@/components/ui/avatar.vue'
import { useUserStore } from '@/store/modules/user'
import { useOaUserStore } from '@/store/modules/oaUser'
import { getLatestNotices, getUnreadCount } from '@/api/notice'
import { getSummary, getTodos } from '@/api/dashboard'
import type { NoticeItem } from '@/api/notice/types'
import type { DashboardSummary, TodoItem } from '@/api/dashboard/types'

const userStore = useUserStore()
const oaUserStore = useOaUserStore()
const router = useRouter()

const notices = ref<NoticeItem[]>([])
const unreadCount = ref(0)
const summary = ref<DashboardSummary | null>(null)
const todos = ref<TodoItem[]>([])
const todoType = ref('')
const loading = ref(false)

const greeting = computed(() => {
  const h = new Date().getHours()
  if (h < 6) return '凌晨好'
  if (h < 11) return '早上好'
  if (h < 13) return '中午好'
  if (h < 18) return '下午好'
  return '晚上好'
})

const todayLabel = computed(() => {
  const d = new Date()
  const wd = ['日', '一', '二', '三', '四', '五', '六'][d.getDay()]
  return `${d.getMonth() + 1}月${d.getDate()}日  星期${wd}`
})

const userInitials = computed(() => {
  const n = userStore.userName || userStore.email || '?'
  return n.slice(0, 2).toUpperCase()
})

const attendanceLabel = computed(() => {
  const t = summary.value?.todayAttendance
  if (!t) return '-'
  if (t.checkedOut) return '已完成'
  if (t.checkedIn) return '已上班'
  return '未打卡'
})

const attendanceVariant = computed(() => {
  const t = summary.value?.todayAttendance
  if (!t) return 'secondary'
  if (t.checkedOut) return 'success'
  if (t.checkedIn) return 'info'
  return 'warning'
})

const statCards = computed(() => [
  {
    icon: ClipboardList,
    value: summary.value?.pendingApprovals ?? 0,
    label: '待我审批',
    path: '/approval/pending',
    color: 'text-primary',
    bg: 'bg-primary-soft',
  },
  {
    icon: Bell,
    value: unreadCount.value,
    label: '未读公告',
    path: '/notice/list',
    color: 'text-warning',
    bg: 'bg-warning/10',
  },
  {
    icon: Clock,
    value: attendanceLabel.value,
    label: '今日考勤',
    path: '/attendance/mine',
    color: 'text-success',
    bg: 'bg-success/10',
  },
  {
    icon: CalendarDays,
    value: summary.value?.todayMeetings ?? 0,
    label: '今日会议',
    path: '/meeting/mine',
    color: 'text-info',
    bg: 'bg-info/10',
  },
])

const quickEntries = [
  { icon: Plus,          label: '发起申请',   path: '/approval/apply',    color: 'text-primary' },
  { icon: Clock,         label: '去打卡',     path: '/attendance/mine',   color: 'text-success' },
  { icon: CalendarDays,  label: '订会议室',   path: '/meeting/calendar',  color: 'text-info' },
  { icon: Users,         label: '通讯录',     path: '/contact',           color: 'text-warning' },
  { icon: Megaphone,     label: '公告中心',   path: '/notice/list',       color: 'text-danger' },
]

const todoTabs = [
  { key: '',           label: '全部' },
  { key: 'approval',   label: '审批' },
  { key: 'notice',     label: '公告' },
  { key: 'attendance', label: '考勤' },
]

function noticeTypeVariant(t: string) {
  return { '1': 'info', '2': 'default', '3': 'danger' }[t] ?? 'default'
}
function noticeTypeLabel(t: string) {
  return { '1': '通知', '2': '公告', '3': '紧急' }[t] ?? t
}
function formatShort(s: string) { return s?.length >= 16 ? s.slice(5, 16) : s }

async function loadTodos() {
  loading.value = true
  try { todos.value = (await getTodos(todoType.value || undefined)).rows }
  finally { loading.value = false }
}

function switchTodoTab(key: string) {
  todoType.value = key
  loadTodos()
}

onMounted(async () => {
  try { summary.value = await getSummary() } catch { /* ignore */ }
  await loadTodos()
  try { notices.value = await getLatestNotices(5) } catch { /* ignore */ }
  try { unreadCount.value = await getUnreadCount() } catch { /* ignore */ }
})
</script>

<template>
  <div>
    <PageHeader :title="`${greeting}，${userStore.userName || userStore.email}`" :sub="todayLabel" />

    <div class="p-5 space-y-4">
      <!-- 4 stat cards -->
      <div class="grid grid-cols-4 gap-3">
        <button
          v-for="card in statCards"
          :key="card.label"
          class="bg-card border border-border rounded-[var(--radius-lg)] p-4 flex items-center gap-3 cursor-pointer hover:border-primary/40 hover:shadow-sm transition-all text-left w-full"
          @click="router.push(card.path)"
        >
          <div :class="['w-10 h-10 rounded-lg flex items-center justify-center shrink-0', card.bg]">
            <component :is="card.icon" :size="20" :class="card.color" />
          </div>
          <div>
            <div class="text-[22px] font-semibold font-mono-tnum leading-tight text-foreground">{{ card.value }}</div>
            <div class="text-[12px] text-muted-foreground mt-0.5">{{ card.label }}</div>
          </div>
        </button>
      </div>

      <div class="grid grid-cols-[1fr_300px] gap-4">
        <!-- Left column: quick entries + todos -->
        <div class="space-y-4">
          <!-- Quick entries -->
          <Card>
            <CardHeader>
              <span class="text-[13px] font-semibold">快捷入口</span>
            </CardHeader>
            <CardContent>
              <div class="grid grid-cols-5 gap-2">
                <button
                  v-for="entry in quickEntries"
                  :key="entry.path"
                  class="flex flex-col items-center gap-1.5 p-3 rounded-[var(--radius)] bg-subtle hover:bg-muted cursor-pointer transition-colors border-0"
                  @click="router.push(entry.path)"
                >
                  <component :is="entry.icon" :size="20" :class="entry.color" />
                  <span class="text-[12px] text-foreground">{{ entry.label }}</span>
                </button>
              </div>
            </CardContent>
          </Card>

          <!-- Todos -->
          <Card>
            <CardHeader>
              <span class="text-[13px] font-semibold">我的待办</span>
              <div class="flex items-center gap-1">
                <button
                  v-for="tab in todoTabs"
                  :key="tab.key"
                  :class="[
                    'px-2.5 py-0.5 rounded text-[11px] cursor-pointer border-0 transition-colors',
                    todoType === tab.key
                      ? 'bg-primary-soft text-primary-soft-fg font-medium'
                      : 'text-muted-foreground hover:bg-muted'
                  ]"
                  @click="switchTodoTab(tab.key)"
                >
                  {{ tab.label }}
                </button>
              </div>
            </CardHeader>
            <div v-if="loading" class="p-8 flex justify-center text-muted-foreground text-[12px]">
              加载中...
            </div>
            <div v-else-if="!todos.length" class="p-8 text-center text-muted-foreground text-[12px]">
              暂无待办
            </div>
            <div v-else class="divide-y divide-border">
              <button
                v-for="t in todos"
                :key="`${t.type}-${t.id}`"
                class="w-full flex items-center gap-3 px-4 py-3 hover:bg-subtle cursor-pointer transition-colors text-left border-0 bg-transparent"
                @click="router.push(t.link)"
              >
                <Badge v-if="t.tag" :variant="(t.tagType as 'info' | 'warning' | 'danger') || 'info'" class="shrink-0">{{ t.tag }}</Badge>
                <div class="flex-1 min-w-0">
                  <div class="text-[13px] font-medium text-foreground truncate">{{ t.title }}</div>
                  <div class="text-[11px] text-muted-foreground mt-0.5">{{ t.subtitle }}</div>
                </div>
                <ArrowRight :size="14" class="shrink-0 text-muted-foreground" />
              </button>
            </div>
          </Card>
        </div>

        <!-- Right column: notices -->
        <Card>
          <CardHeader>
            <span class="text-[13px] font-semibold">最新公告</span>
            <button
              class="text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent"
              @click="router.push('/notice/list')"
            >
              查看全部
            </button>
          </CardHeader>
          <div v-if="!notices.length" class="p-8 text-center text-muted-foreground text-[12px]">
            暂无公告
          </div>
          <div v-else class="divide-y divide-border">
            <button
              v-for="n in notices"
              :key="n.noticeId"
              class="w-full flex items-start gap-2 px-4 py-3 hover:bg-subtle cursor-pointer transition-colors text-left border-0 bg-transparent"
              @click="router.push(`/notice/${n.noticeId}`)"
            >
              <Badge :variant="noticeTypeVariant(n.noticeType) as 'info' | 'default' | 'danger'" class="shrink-0 mt-0.5">
                {{ noticeTypeLabel(n.noticeType) }}
              </Badge>
              <div class="flex-1 min-w-0">
                <div :class="['text-[12px] truncate', !n.isRead ? 'font-medium text-foreground' : 'text-muted-foreground']">
                  {{ n.title }}
                </div>
                <div class="text-[11px] text-muted-foreground mt-0.5 font-mono-tnum">{{ formatShort(n.publishTime) }}</div>
              </div>
              <div v-if="!n.isRead" class="w-1.5 h-1.5 rounded-full bg-danger shrink-0 mt-1.5" />
            </button>
          </div>
        </Card>
      </div>
    </div>
  </div>
</template>
