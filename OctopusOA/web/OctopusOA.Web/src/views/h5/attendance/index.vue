<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { CheckCircle2, XCircle, Clock, AlertCircle } from '@lucide/vue'
import { checkIn, checkOut, getToday, getMine } from '@/api/attendance'
import type { TodayAttendance, AttendanceItem } from '@/api/attendance/types'

const today = ref<TodayAttendance | null>(null)
const monthRecords = ref<AttendanceItem[]>([])
const loadingToday = ref(true)
const loadingMonth = ref(true)
const actionLoading = ref(false)
const actionError = ref('')
const actionSuccess = ref('')

const currentMonth = computed(() => {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
})

function statusIcon(status: string) {
  return { normal: CheckCircle2, late: AlertCircle, early: AlertCircle, missing: XCircle, weekend: Clock }[status] ?? Clock
}

function statusClass(status: string) {
  return {
    normal: 'text-success',
    late: 'text-warning',
    early: 'text-warning',
    missing: 'text-danger',
    weekend: 'text-muted-foreground',
  }[status] ?? 'text-muted-foreground'
}

function statusLabel(status: string) {
  return { normal: '正常', late: '迟到', early: '早退', missing: '缺卡', weekend: '休息' }[status] ?? status
}

async function handleCheckIn() {
  actionLoading.value = true
  actionError.value = ''
  actionSuccess.value = ''
  try {
    await checkIn()
    actionSuccess.value = '打卡成功！'
    today.value = await getToday()
  } catch (e: unknown) {
    actionError.value = (e as Error).message || '打卡失败'
  } finally {
    actionLoading.value = false
  }
}

async function handleCheckOut() {
  actionLoading.value = true
  actionError.value = ''
  actionSuccess.value = ''
  try {
    await checkOut()
    actionSuccess.value = '下班打卡成功！'
    today.value = await getToday()
  } catch (e: unknown) {
    actionError.value = (e as Error).message || '打卡失败'
  } finally {
    actionLoading.value = false
  }
}

// Mini calendar: generate days for current month
const calendarDays = computed(() => {
  const now = new Date()
  const year = now.getFullYear()
  const month = now.getMonth()
  const daysInMonth = new Date(year, month + 1, 0).getDate()
  const firstDay = new Date(year, month, 1).getDay()
  const days: { day: number | null; record?: AttendanceItem }[] = []
  // padding
  for (let i = 0; i < firstDay; i++) days.push({ day: null })
  for (let d = 1; d <= daysInMonth; d++) {
    const dateStr = `${year}-${String(month + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`
    const record = monthRecords.value.find(r => r.date.startsWith(dateStr))
    days.push({ day: d, record })
  }
  return days
})

onMounted(async () => {
  loadingToday.value = true
  loadingMonth.value = true
  try {
    today.value = await getToday()
  } finally {
    loadingToday.value = false
  }
  try {
    const res = await getMine(currentMonth.value)
    monthRecords.value = res.rows
  } finally {
    loadingMonth.value = false
  }
})
</script>

<template>
  <div class="min-h-full">
    <!-- Header -->
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4">
        <h1 class="text-[17px] font-semibold text-foreground">考勤</h1>
      </div>
    </div>

    <div class="p-4 space-y-4">
      <!-- Clock-in card -->
      <div class="bg-gradient-to-br from-primary to-[#3949ab] rounded-2xl p-5 text-white">
        <div v-if="loadingToday" class="py-4 text-center text-white/70 text-[13px]">加载中...</div>
        <template v-else-if="today">
          <div class="flex items-start justify-between mb-4">
            <div>
              <p class="text-white/70 text-[12px]">今日考勤</p>
              <p class="text-white text-[13px] mt-1">工作时间 {{ today.ruleWorkStart }} — {{ today.ruleWorkEnd }}</p>
            </div>
            <div class="text-right">
              <p class="text-[11px] text-white/60">{{ new Date().toLocaleDateString('zh-CN', { month: 'long', day: 'numeric', weekday: 'short' }) }}</p>
            </div>
          </div>

          <!-- Check-in / Check-out status -->
          <div class="grid grid-cols-2 gap-3 mb-4">
            <div class="bg-white/10 rounded-xl p-3">
              <p class="text-white/60 text-[11px] mb-1">上班打卡</p>
              <p class="text-white font-semibold text-[16px]">{{ today.checkInTime || '—' }}</p>
              <p class="text-[11px] mt-1" :class="today.checkInStatus === 'normal' ? 'text-green-300' : 'text-yellow-300'">
                {{ today.checkInTime ? statusLabel(today.checkInStatus) : '未打卡' }}
              </p>
            </div>
            <div class="bg-white/10 rounded-xl p-3">
              <p class="text-white/60 text-[11px] mb-1">下班打卡</p>
              <p class="text-white font-semibold text-[16px]">{{ today.checkOutTime || '—' }}</p>
              <p class="text-[11px] mt-1" :class="today.checkOutTime ? 'text-green-300' : 'text-white/50'">
                {{ today.checkOutTime ? statusLabel(today.checkOutStatus) : '未打卡' }}
              </p>
            </div>
          </div>

          <!-- Action -->
          <p v-if="actionError" class="text-red-300 text-[12px] mb-2">{{ actionError }}</p>
          <p v-if="actionSuccess" class="text-green-300 text-[12px] mb-2">{{ actionSuccess }}</p>
          <div class="flex gap-3">
            <button
              v-if="today.canCheckIn"
              :disabled="actionLoading"
              class="flex-1 h-11 bg-white text-primary text-[14px] font-semibold rounded-xl hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border-0"
              @click="handleCheckIn"
            >
              {{ actionLoading ? '处理中...' : '上班打卡' }}
            </button>
            <button
              v-if="today.canCheckOut"
              :disabled="actionLoading"
              class="flex-1 h-11 bg-white/20 text-white text-[14px] font-semibold rounded-xl hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border border-white/30"
              @click="handleCheckOut"
            >
              {{ actionLoading ? '处理中...' : '下班打卡' }}
            </button>
            <div v-if="!today.canCheckIn && !today.canCheckOut" class="w-full text-center text-white/60 text-[13px] py-2">
              今日打卡已完成 ✓
            </div>
          </div>
        </template>
      </div>

      <!-- Mini calendar -->
      <div class="bg-background border border-border rounded-xl p-4">
        <h3 class="text-[13px] font-semibold text-foreground mb-3">本月考勤</h3>

        <!-- Weekday headers -->
        <div class="grid grid-cols-7 gap-1 mb-2">
          <div v-for="d in ['日','一','二','三','四','五','六']" :key="d" class="text-center text-[10px] text-muted-foreground">{{ d }}</div>
        </div>

        <!-- Days -->
        <div class="grid grid-cols-7 gap-1">
          <div
            v-for="(cell, idx) in calendarDays"
            :key="idx"
            class="aspect-square flex flex-col items-center justify-center rounded-lg text-[12px]"
            :class="[
              cell.day === new Date().getDate() ? 'bg-primary text-primary-foreground font-semibold' :
              cell.record?.checkInStatus === 'missing' ? 'bg-[color-mix(in_oklch,var(--danger)_10%,transparent)]' :
              cell.record ? 'bg-[color-mix(in_oklch,var(--success)_10%,transparent)]' : ''
            ]"
          >
            <span v-if="cell.day" class="leading-none">{{ cell.day }}</span>
            <component
              v-if="cell.record && cell.day !== new Date().getDate()"
              :is="statusIcon(cell.record.checkInStatus)"
              :size="8"
              :class="statusClass(cell.record.checkInStatus)"
              class="mt-0.5"
            />
          </div>
        </div>
      </div>

      <!-- Recent records -->
      <div class="bg-background border border-border rounded-xl overflow-hidden">
        <div class="px-4 py-3 border-b border-border">
          <h3 class="text-[13px] font-semibold text-foreground">最近记录</h3>
        </div>
        <div v-if="loadingMonth" class="py-6 text-center text-muted-foreground text-[12px]">加载中...</div>
        <div v-else-if="!monthRecords.length" class="py-6 text-center text-muted-foreground text-[12px]">暂无记录</div>
        <div v-else class="divide-y divide-border">
          <div
            v-for="record in monthRecords.slice(0, 7)"
            :key="record.id"
            class="flex items-center px-4 py-3 gap-3"
          >
            <component :is="statusIcon(record.checkInStatus)" :size="16" :class="statusClass(record.checkInStatus)" />
            <div class="flex-1">
              <p class="text-[13px] text-foreground">{{ record.date }}</p>
              <p class="text-[11px] text-muted-foreground mt-0.5">
                {{ record.checkInTime || '—' }} / {{ record.checkOutTime || '—' }}
              </p>
            </div>
            <span :class="['text-[11px] font-medium', statusClass(record.checkInStatus)]">
              {{ statusLabel(record.checkInStatus) }}
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
