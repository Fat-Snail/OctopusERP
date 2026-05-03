<script setup lang="ts">
import { ref, onMounted } from 'vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'

import { toast } from '@/components/ui/toast'
import { checkIn, checkOut, getToday, getMine } from '@/api/attendance'
import type { TodayAttendance, AttendanceItem, CheckStatus } from '@/api/attendance/types'
import AttendanceCalendar from '../components/AttendanceCalendar.vue'

const today = ref<TodayAttendance | null>(null)
const list = ref<AttendanceItem[]>([])
const loading = ref(false)
const submitting = ref(false)
const month = ref(new Date().toISOString().slice(0, 7))
const viewMode = ref<'calendar' | 'list'>('calendar')
const detailVisible = ref(false)
const selected = ref<AttendanceItem | null>(null)

async function loadToday() {
  try { today.value = await getToday() } catch { /* ignore */ }
}

async function loadMine() {
  loading.value = true
  try { list.value = (await getMine(month.value)).rows } finally { loading.value = false }
}

async function handleCheckIn() {
  submitting.value = true
  try { await checkIn(); toast('上班打卡成功', 'success'); await loadToday(); await loadMine() }
  catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

async function handleCheckOut() {
  submitting.value = true
  try { await checkOut(); toast('下班打卡成功', 'success'); await loadToday(); await loadMine() }
  catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

function openDetail(item: AttendanceItem | null) {
  if (!item) return
  selected.value = item
  detailVisible.value = true
}

type BadgeVariant = 'success' | 'danger' | 'secondary'
function statusVariant(s: CheckStatus): BadgeVariant {
  return { normal: 'success', late: 'danger', early: 'danger', missing: 'secondary', weekend: 'secondary' }[s] as BadgeVariant ?? 'secondary'
}
function statusLabel(s: CheckStatus): string {
  return { normal: '正常', late: '迟到', early: '早退', missing: '未打卡', weekend: '休息' }[s] ?? s
}
function weekday(dateStr: string): string {
  const d = new Date(dateStr + 'T00:00:00')
  return ['日', '一', '二', '三', '四', '五', '六'][d.getDay()]
}

onMounted(async () => { await loadToday(); await loadMine() })
</script>

<template>
  <div>
    <PageHeader title="我的考勤" />

    <div class="p-5 space-y-4">
      <!-- Today punch card -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
        <div class="flex items-center justify-between mb-3">
          <span class="text-[12px] font-semibold text-foreground">今日打卡</span>
          <span class="text-[11px] text-muted-foreground">
            {{ today?.date }} · 班次 {{ today?.ruleWorkStart }}–{{ today?.ruleWorkEnd }}
          </span>
        </div>
        <div v-if="today" class="flex items-center gap-4 justify-center py-3">
          <div :class="['flex-1 max-w-[260px] text-center p-5 rounded-[var(--radius-lg)] border-2 transition-all', today.checkInTime ? 'border-success bg-success/5' : 'border-dashed border-border bg-subtle']">
            <div class="text-[12px] text-muted-foreground mb-1">上班打卡</div>
            <div class="text-[22px] font-bold font-mono mb-2">{{ today.checkInTime?.slice(11, 16) || '未打卡' }}</div>
            <Badge v-if="today.checkInTime" :variant="statusVariant(today.checkInStatus as CheckStatus)">{{ statusLabel(today.checkInStatus as CheckStatus) }}</Badge>
            <button v-else :disabled="submitting" class="h-[var(--row-h)] px-4 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50" @click="handleCheckIn">
              打上班卡
            </button>
          </div>

          <div class="text-[20px] text-muted-foreground">→</div>

          <div :class="['flex-1 max-w-[260px] text-center p-5 rounded-[var(--radius-lg)] border-2 transition-all', today.checkOutTime ? 'border-success bg-success/5' : 'border-dashed border-border bg-subtle']">
            <div class="text-[12px] text-muted-foreground mb-1">下班打卡</div>
            <div class="text-[22px] font-bold font-mono mb-2">{{ today.checkOutTime?.slice(11, 16) || '未打卡' }}</div>
            <Badge v-if="today.checkOutTime" :variant="statusVariant(today.checkOutStatus as CheckStatus)">{{ statusLabel(today.checkOutStatus as CheckStatus) }}</Badge>
            <button v-else :disabled="submitting || !today.canCheckOut" class="h-[var(--row-h)] px-4 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50" @click="handleCheckOut">
              打下班卡
            </button>
          </div>
        </div>
      </div>

      <!-- Monthly attendance -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
        <div class="flex items-center justify-between mb-3">
          <span class="text-[12px] font-semibold text-foreground">月度考勤</span>
          <div class="flex items-center gap-2">
            <div class="flex gap-0 border border-border rounded-[var(--radius)] overflow-hidden text-[11px]">
              <button
                :class="['px-3 py-1 cursor-pointer border-0 transition-colors', viewMode === 'calendar' ? 'bg-primary text-primary-foreground' : 'bg-card text-muted-foreground hover:bg-muted']"
                @click="viewMode = 'calendar'"
              >日历</button>
              <button
                :class="['px-3 py-1 cursor-pointer border-0 transition-colors', viewMode === 'list' ? 'bg-primary text-primary-foreground' : 'bg-card text-muted-foreground hover:bg-muted']"
                @click="viewMode = 'list'"
              >列表</button>
            </div>
            <input
              v-model="month"
              type="month"
              class="h-[var(--row-h)] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring"
              @change="loadMine"
            />
          </div>
        </div>

        <div v-if="loading" class="py-8 text-center text-muted-foreground text-[12px]">加载中...</div>
        <AttendanceCalendar v-else-if="viewMode === 'calendar'" :month="month" :records="list" @select="openDetail" />
        <div v-else class="border border-border rounded-[var(--radius)] overflow-hidden">
          <table class="w-full text-[12px]">
            <thead class="bg-subtle">
              <tr>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground w-24">日期</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground w-14">星期</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground">上班打卡</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground w-20">状态</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground">下班打卡</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground w-20">状态</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground w-16">工时</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border">
              <tr v-for="row in list" :key="row.date" class="hover:bg-subtle">
                <td class="px-3 py-2 font-mono-tnum">{{ row.date }}</td>
                <td class="px-3 py-2 text-muted-foreground">{{ weekday(row.date) }}</td>
                <td class="px-3 py-2 font-mono-tnum text-muted-foreground">{{ row.checkInTime?.slice(11, 16) || '-' }}</td>
                <td class="px-3 py-2"><Badge :variant="statusVariant(row.checkInStatus as CheckStatus)">{{ statusLabel(row.checkInStatus as CheckStatus) }}</Badge></td>
                <td class="px-3 py-2 font-mono-tnum text-muted-foreground">{{ row.checkOutTime?.slice(11, 16) || '-' }}</td>
                <td class="px-3 py-2"><Badge :variant="statusVariant(row.checkOutStatus as CheckStatus)">{{ statusLabel(row.checkOutStatus as CheckStatus) }}</Badge></td>
                <td class="px-3 py-2 font-mono-tnum">{{ row.workHours ? row.workHours.toFixed(1) : '-' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <Dialog v-model:open="detailVisible">
      <DialogContent class="p-5 w-[380px]">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">打卡详情</DialogTitle>
        </DialogHeader>
        <div v-if="selected" class="space-y-2 text-[12px]">
          <div class="flex justify-between py-1.5 border-b border-border">
            <span class="text-muted-foreground">日期</span>
            <span>{{ selected.date }}（{{ weekday(selected.date) }}）</span>
          </div>
          <div class="flex justify-between py-1.5 border-b border-border">
            <span class="text-muted-foreground">上班打卡</span>
            <span class="font-mono-tnum">{{ selected.checkInTime?.slice(11, 16) || '-' }}</span>
          </div>
          <div class="flex justify-between py-1.5 border-b border-border">
            <span class="text-muted-foreground">上班状态</span>
            <Badge :variant="statusVariant(selected.checkInStatus as CheckStatus)">{{ statusLabel(selected.checkInStatus as CheckStatus) }}</Badge>
          </div>
          <div class="flex justify-between py-1.5 border-b border-border">
            <span class="text-muted-foreground">下班打卡</span>
            <span class="font-mono-tnum">{{ selected.checkOutTime?.slice(11, 16) || '-' }}</span>
          </div>
          <div class="flex justify-between py-1.5 border-b border-border">
            <span class="text-muted-foreground">下班状态</span>
            <Badge :variant="statusVariant(selected.checkOutStatus as CheckStatus)">{{ statusLabel(selected.checkOutStatus as CheckStatus) }}</Badge>
          </div>
          <div class="flex justify-between py-1.5 border-b border-border">
            <span class="text-muted-foreground">工时</span>
            <span>{{ selected.workHours ? selected.workHours.toFixed(2) + ' 小时' : '-' }}</span>
          </div>
          <div class="flex justify-between py-1.5">
            <span class="text-muted-foreground">是否补卡</span>
            <Badge v-if="selected.isFixed" variant="warning">已补卡</Badge>
            <span v-else class="text-muted-foreground">-</span>
          </div>
        </div>
        <div class="flex justify-end mt-4">
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="detailVisible = false">关闭</button>
        </div>
      </DialogContent>
    </Dialog>
  </div>
</template>
