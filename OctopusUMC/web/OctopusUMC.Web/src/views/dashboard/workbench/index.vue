<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { Users, TrendingUp, Bell, UserCheck } from 'lucide-vue-next'
import { useUserStore } from '@/store/modules/user'
import { getDashboardSummary, getOperlogList, getLogininforList } from '@/api/monitor/operlog'
import { getNoticeList } from '@/api/system/notice'
import { startOnlineUserHub, stopOnlineUserHub } from '@/utils/hubService'
import type { OperlogResponse, LoginfoResponse, DashboardSummary } from '@/api/monitor/types'
import type { NoticeResponse } from '@/api/system/types'
import Badge from '@/components/ui/badge.vue'

const userStore = useUserStore()

const today = new Date().toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric', weekday: 'long' })

const summary = ref<DashboardSummary>({ onlineUserCount: 0, todayLoginCount: 0, noticeCount: 0, totalUserCount: 0 })
const recentLogs = ref<OperlogResponse[]>([])
const recentLogins = ref<LoginfoResponse[]>([])
const notices = ref<NoticeResponse[]>([])
const activeTab = ref<'operlog' | 'logininfor'>('operlog')

const stats = [
  { label: '在线用户', key: 'onlineUserCount' as const, icon: Users, color: 'text-primary', bg: 'bg-primary-soft' },
  { label: '今日登录', key: 'todayLoginCount' as const, icon: TrendingUp, color: 'text-[var(--success)]', bg: 'bg-[color-mix(in_oklch,var(--success)_12%,transparent)]' },
  { label: '系统公告', key: 'noticeCount' as const, icon: Bell, color: 'text-[var(--warning)]', bg: 'bg-[color-mix(in_oklch,var(--warning)_12%,transparent)]' },
  { label: '在职用户', key: 'totalUserCount' as const, icon: UserCheck, color: 'text-[var(--info)]', bg: 'bg-[color-mix(in_oklch,var(--info)_12%,transparent)]' },
]

async function loadSummary() {
  try { summary.value = await getDashboardSummary() } catch { /* ignore */ }
}

async function loadNotices() {
  try {
    const r = await getNoticeList({ pageNum: 1, pageSize: 4 })
    notices.value = r.rows
  } catch { /* ignore */ }
}

onMounted(async () => {
  await loadSummary()
  try {
    const r = await getOperlogList({ pageNum: 1, pageSize: 5 })
    recentLogs.value = r.rows
  } catch { /* ignore */ }
  try {
    const r = await getLogininforList({ pageNum: 1, pageSize: 5 })
    recentLogins.value = r.rows
  } catch { /* ignore */ }
  await loadNotices()

  try {
    const hub = await startOnlineUserHub()
    hub.on('UserConnected', loadSummary)
    hub.on('UserDisconnected', loadSummary)
    hub.on('NewNotice', async (notice: NoticeResponse) => {
      // prepend to list and bump counter
      notices.value = [notice, ...notices.value].slice(0, 4)
      summary.value = { ...summary.value, noticeCount: summary.value.noticeCount + 1 }
    })
  } catch { /* hub unavailable in tests */ }
})

onUnmounted(async () => {
  try {
    const { getOnlineUserHub } = await import('@/utils/hubService')
    const hub = getOnlineUserHub()
    hub.off('UserConnected', loadSummary)
    hub.off('UserDisconnected', loadSummary)
    hub.off('NewNotice')
  } catch { /* ignore */ }
  await stopOnlineUserHub()
})
</script>

<template>
  <div class="space-y-4">
    <!-- Welcome card -->
    <div class="bg-card border border-border rounded-lg p-5 flex items-center gap-4">
      <div class="w-14 h-14 rounded-full bg-primary-soft flex items-center justify-center shrink-0">
        <span class="text-[var(--primary-soft-fg)] font-semibold text-[18px]">
          {{ (userStore.userInfo?.nickName || userStore.userInfo?.userName || 'A').slice(0, 1).toUpperCase() }}
        </span>
      </div>
      <div class="flex-1">
        <h2 class="text-[18px] font-semibold text-foreground">
          欢迎回来，{{ userStore.userInfo?.nickName || userStore.userInfo?.userName || 'Admin' }}！
        </h2>
        <p class="text-[13px] text-muted-foreground mt-0.5">今天是 {{ today }}，祝您工作愉快。</p>
      </div>
    </div>

    <!-- Stats cards -->
    <div class="grid grid-cols-4 gap-4">
      <div
        v-for="item in stats"
        :key="item.label"
        class="bg-card border border-border rounded-lg p-5 flex items-center gap-4"
      >
        <div class="w-12 h-12 rounded-lg flex items-center justify-center shrink-0" :class="item.bg">
          <component :is="item.icon" :size="24" :class="item.color" />
        </div>
        <div>
          <div class="text-[28px] font-bold text-foreground font-mono-tnum leading-none">{{ summary[item.key] }}</div>
          <div class="text-[12px] text-muted-foreground mt-1">{{ item.label }}</div>
        </div>
      </div>
    </div>

    <!-- Bottom two columns -->
    <div class="grid grid-cols-[1fr_360px] gap-4">
      <!-- Log tabs -->
      <div class="bg-card border border-border rounded-lg overflow-hidden">
        <div class="px-4 py-2.5 border-b border-border flex items-center gap-4">
          <button
            class="text-[13px] font-medium pb-0.5 border-b-2 transition-colors"
            :class="activeTab === 'operlog' ? 'border-primary text-foreground' : 'border-transparent text-muted-foreground hover:text-foreground'"
            @click="activeTab = 'operlog'"
          >操作日志</button>
          <button
            class="text-[13px] font-medium pb-0.5 border-b-2 transition-colors"
            :class="activeTab === 'logininfor' ? 'border-primary text-foreground' : 'border-transparent text-muted-foreground hover:text-foreground'"
            @click="activeTab = 'logininfor'"
          >访问日志</button>
        </div>

        <!-- Operlog table -->
        <div v-if="activeTab === 'operlog'" class="overflow-x-auto">
          <table class="w-full">
            <thead>
              <tr class="bg-subtle border-b border-border">
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">操作人</th>
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">模块</th>
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">方式</th>
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">耗时(ms)</th>
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">状态</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="log in recentLogs"
                :key="log.operId"
                class="border-b border-border last:border-0 hover:bg-subtle"
              >
                <td class="px-3 py-2 text-[12px] text-foreground">{{ log.operName }}</td>
                <td class="px-3 py-2 text-[12px] text-foreground">{{ log.title }}</td>
                <td class="px-3 py-2 text-[12px] text-foreground font-mono-tnum">{{ log.requestMethod }}</td>
                <td class="px-3 py-2 text-[12px] text-muted-foreground font-mono-tnum">{{ log.costTime }}</td>
                <td class="px-3 py-2">
                  <Badge :variant="log.status === 0 ? 'success' : 'danger'">
                    {{ log.status === 0 ? '成功' : '失败' }}
                  </Badge>
                </td>
              </tr>
              <tr v-if="!recentLogs.length">
                <td colspan="5" class="px-3 py-6 text-center text-[12px] text-muted-foreground">暂无操作日志</td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Logininfor table -->
        <div v-if="activeTab === 'logininfor'" class="overflow-x-auto">
          <table class="w-full">
            <thead>
              <tr class="bg-subtle border-b border-border">
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">用户名</th>
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">IP地址</th>
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">登录时间</th>
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">状态</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="log in recentLogins"
                :key="log.infoId"
                class="border-b border-border last:border-0 hover:bg-subtle"
              >
                <td class="px-3 py-2 text-[12px] text-foreground">{{ log.userName }}</td>
                <td class="px-3 py-2 text-[12px] text-muted-foreground font-mono-tnum">{{ log.ipaddr }}</td>
                <td class="px-3 py-2 text-[12px] text-muted-foreground">{{ log.loginTime }}</td>
                <td class="px-3 py-2">
                  <Badge :variant="log.status === 0 ? 'danger' : 'success'">
                    {{ log.status === 0 ? '失败' : '成功' }}
                  </Badge>
                </td>
              </tr>
              <tr v-if="!recentLogins.length">
                <td colspan="4" class="px-3 py-6 text-center text-[12px] text-muted-foreground">暂无访问日志</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- System notices -->
      <div class="bg-card border border-border rounded-lg overflow-hidden">
        <div class="px-4 py-3 border-b border-border">
          <span class="text-[13px] font-semibold text-foreground">系统公告</span>
        </div>
        <ul class="divide-y divide-border">
          <li
            v-for="n in notices"
            :key="n.noticeId"
            class="flex items-center gap-2.5 px-4 py-3 hover:bg-subtle"
          >
            <Badge :variant="n.noticeType === '1' ? 'warning' : 'info'">
              {{ n.noticeType === '1' ? '通知' : '公告' }}
            </Badge>
            <span class="flex-1 text-[12px] text-foreground truncate">{{ n.noticeTitle }}</span>
            <span class="text-[11px] text-muted-foreground shrink-0">{{ n.createTime.slice(0, 10) }}</span>
          </li>
          <li v-if="!notices.length" class="px-4 py-6 text-center text-[12px] text-muted-foreground">
            暂无公告
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>
