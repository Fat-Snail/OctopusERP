<script setup lang="ts">
import { ref, onMounted } from 'vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { getStats, getAbnormal } from '@/api/attendance'
import type { AttendanceStats, AttendanceItem } from '@/api/attendance/types'

const month = ref(new Date().toISOString().slice(0, 7))
const tab = ref<'stats' | 'abnormal'>('stats')
const loading = ref(false)
const stats = ref<AttendanceStats[]>([])
const abnormalList = ref<AttendanceItem[]>([])

async function loadAll() {
  loading.value = true
  try {
    stats.value = (await getStats(month.value)).rows
    abnormalList.value = (await getAbnormal(month.value)).rows
  } finally { loading.value = false }
}

onMounted(loadAll)
</script>

<template>
  <div>
    <PageHeader title="考勤统计" />

    <div class="p-5">
      <div class="flex items-center gap-3 mb-4">
        <label class="text-[12px] text-muted-foreground">月份：</label>
        <input
          v-model="month"
          type="month"
          class="h-[var(--row-h)] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring"
          @change="loadAll"
        />
      </div>

      <div class="flex gap-0 border border-border rounded-[var(--radius)] overflow-hidden w-fit mb-4 text-[12px]">
        <button :class="['px-4 py-1.5 cursor-pointer border-0 transition-colors', tab === 'stats' ? 'bg-primary text-primary-foreground' : 'bg-card text-muted-foreground hover:bg-muted']" @click="tab = 'stats'">员工统计</button>
        <button :class="['px-4 py-1.5 cursor-pointer border-0 transition-colors', tab === 'abnormal' ? 'bg-primary text-primary-foreground' : 'bg-card text-muted-foreground hover:bg-muted']" @click="tab = 'abnormal'">异常考勤</button>
      </div>

      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>

      <!-- Stats table -->
      <div v-else-if="tab === 'stats'" class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">姓名</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">用户名</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">正常天数</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">迟到次数</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">早退次数</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-16">缺勤</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">总工时</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in stats" :key="row.umcUserId" class="hover:bg-subtle">
              <td class="px-3 py-2 font-medium">{{ row.nickName }}</td>
              <td class="px-3 py-2 text-muted-foreground">{{ row.userName }}</td>
              <td class="px-3 py-2 font-mono-tnum">{{ row.normalDays }}</td>
              <td class="px-3 py-2">
                <Badge v-if="row.lateCount > 0" variant="danger">{{ row.lateCount }}</Badge>
                <span v-else class="text-muted-foreground">0</span>
              </td>
              <td class="px-3 py-2">
                <Badge v-if="row.earlyLeaveCount > 0" variant="danger">{{ row.earlyLeaveCount }}</Badge>
                <span v-else class="text-muted-foreground">0</span>
              </td>
              <td class="px-3 py-2">
                <Badge v-if="row.missingCount > 0" variant="warning">{{ row.missingCount }}</Badge>
                <span v-else class="text-muted-foreground">0</span>
              </td>
              <td class="px-3 py-2 font-mono-tnum">{{ row.totalWorkHours.toFixed(1) }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Abnormal table -->
      <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">日期</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">姓名</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">上班打卡</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">状态</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">下班打卡</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">状态</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in abnormalList" :key="row.date + (row as unknown as { umcUserId?: number }).umcUserId" class="hover:bg-subtle">
              <td class="px-3 py-2 font-mono-tnum">{{ row.date }}</td>
              <td class="px-3 py-2">{{ (row as unknown as { nickName?: string }).nickName }}</td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground">{{ row.checkInTime?.slice(11, 16) || '-' }}</td>
              <td class="px-3 py-2">
                <Badge :variant="row.checkInStatus === 'late' ? 'danger' : 'success'">{{ row.checkInStatus === 'late' ? '迟到' : '正常' }}</Badge>
              </td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground">{{ row.checkOutTime?.slice(11, 16) || '-' }}</td>
              <td class="px-3 py-2">
                <Badge :variant="row.checkOutStatus === 'early' ? 'danger' : 'success'">{{ row.checkOutStatus === 'early' ? '早退' : '正常' }}</Badge>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
