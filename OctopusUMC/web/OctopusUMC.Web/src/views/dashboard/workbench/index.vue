<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Users, TrendingUp, Bell, CheckSquare } from 'lucide-vue-next'
import { useUserStore } from '@/store/modules/user'
import { get } from '@/utils/http'
import type { PagedResult } from '@/api/types'
import type { OperlogResponse } from '@/api/monitor/types'
import type { NoticeResponse } from '@/api/system/types'
import Badge from '@/components/ui/badge.vue'

const userStore = useUserStore()

const today = new Date().toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric', weekday: 'long' })

const stats = [
  { label: '在线用户', value: 18, icon: Users, color: 'text-primary' },
  { label: '今日登录', value: 47, icon: TrendingUp, color: 'text-success' },
  { label: '系统公告', value: 5, icon: Bell, color: 'text-warning' },
  { label: '待处理', value: 0, icon: CheckSquare, color: 'text-danger' },
]

const recentLogs = ref<OperlogResponse[]>([])
const notices = ref<NoticeResponse[]>([])

onMounted(async () => {
  try {
    const logData = await get<PagedResult<OperlogResponse>>('/monitor/operlog', { pageNum: 1, pageSize: 5 })
    recentLogs.value = logData.rows
  } catch { /* ignore */ }
  try {
    const noticeData = await get<PagedResult<NoticeResponse>>('/tool/notice', { pageNum: 1, pageSize: 3 })
    notices.value = noticeData.rows
  } catch { /* ignore */ }
})
</script>

<template>
  <div class="space-y-4">
    <!-- Welcome card -->
    <div class="bg-card border border-border rounded-lg p-5 flex items-center gap-4">
      <div class="w-14 h-14 rounded-full bg-primary-soft flex items-center justify-center shrink-0">
        <span class="text-primary-soft-fg font-semibold text-[18px]">
          {{ (userStore.userInfo?.nickName || userStore.userInfo?.userName || 'A').slice(0, 1).toUpperCase() }}
        </span>
      </div>
      <div>
        <h2 class="text-[18px] font-semibold text-foreground">
          欢迎回来，{{ userStore.userInfo?.nickName || userStore.userInfo?.userName || 'Admin' }} !
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
        <div class="w-12 h-12 rounded-lg bg-muted flex items-center justify-center shrink-0">
          <component :is="item.icon" :size="24" :class="item.color" />
        </div>
        <div>
          <div class="text-[28px] font-bold text-foreground font-mono-tnum leading-none">{{ item.value }}</div>
          <div class="text-[12px] text-muted-foreground mt-1">{{ item.label }}</div>
        </div>
      </div>
    </div>

    <!-- Bottom two columns -->
    <div class="grid grid-cols-[1fr_380px] gap-4">
      <!-- Recent operation logs -->
      <div class="bg-card border border-border rounded-lg overflow-hidden">
        <div class="px-4 py-3 border-b border-border">
          <span class="text-[13px] font-semibold text-foreground">近期操作日志</span>
        </div>
        <div class="overflow-x-auto">
          <table class="w-full">
            <thead>
              <tr class="bg-subtle border-b border-border">
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">操作人</th>
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">模块</th>
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">方式</th>
                <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2 font-medium">时间</th>
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
                <td class="px-3 py-2 text-[12px] text-muted-foreground">{{ log.operTime }}</td>
                <td class="px-3 py-2">
                  <Badge :variant="log.status === 0 ? 'success' : 'danger'">
                    {{ log.status === 0 ? '成功' : '失败' }}
                  </Badge>
                </td>
              </tr>
              <tr v-if="!recentLogs.length">
                <td colspan="5" class="px-3 py-6 text-center text-[12px] text-muted-foreground">暂无数据</td>
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
