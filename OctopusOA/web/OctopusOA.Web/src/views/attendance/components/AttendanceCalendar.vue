<script setup lang="ts">
import { computed } from 'vue'
import type { AttendanceItem } from '@/api/attendance/types'

const props = defineProps<{ month: string; records: AttendanceItem[] }>()
defineEmits<{ (e: 'select', record: AttendanceItem | null): void }>()

const weekdays = ['一', '二', '三', '四', '五', '六', '日']

interface Cell {
  date: string | null
  day: number
  isToday: boolean
  isWeekend: boolean
  isPast: boolean
  record: AttendanceItem | null
}

const cells = computed<Cell[]>(() => {
  const [yStr, mStr] = props.month.split('-')
  const year = Number(yStr)
  const month = Number(mStr)
  const firstDay = new Date(year, month - 1, 1)
  const lastDay = new Date(year, month, 0)
  const daysInMonth = lastDay.getDate()
  const firstWeekday = (firstDay.getDay() + 6) % 7

  const todayStr = new Date().toISOString().slice(0, 10)
  const recordMap = new Map(props.records.map(r => [r.date, r]))
  const list: Cell[] = []

  for (let i = 0; i < firstWeekday; i++) {
    list.push({ date: null, day: 0, isToday: false, isWeekend: false, isPast: false, record: null })
  }

  for (let d = 1; d <= daysInMonth; d++) {
    const dateObj = new Date(year, month - 1, d)
    const dateStr = `${year}-${String(month).padStart(2, '0')}-${String(d).padStart(2, '0')}`
    const dow = dateObj.getDay()
    const isWeekend = dow === 0 || dow === 6
    const isPast = dateStr < todayStr
    const record = recordMap.get(dateStr) ?? null
    list.push({
      date: dateStr, day: d,
      isToday: dateStr === todayStr,
      isWeekend, isPast,
      record: record && (record.checkInTime || record.checkOutTime) ? record : null,
    })
  }

  while (list.length % 7 !== 0) {
    list.push({ date: null, day: 0, isToday: false, isWeekend: false, isPast: false, record: null })
  }

  return list
})

function shortTime(iso: string | null): string {
  if (!iso) return ''
  if (iso.length >= 16) return iso.slice(11, 16)
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

type CheckStatus = 'normal' | 'late' | 'early' | 'missing' | 'weekend'
function dotColor(s: CheckStatus): string {
  return { normal: 'bg-success', late: 'bg-danger', early: 'bg-danger', missing: 'bg-muted-foreground', weekend: 'bg-border' }[s] ?? 'bg-border'
}
</script>

<template>
  <div class="py-2">
    <!-- Legend -->
    <div class="flex flex-wrap gap-4 text-[11px] text-muted-foreground mb-3">
      <span class="flex items-center gap-1"><i class="w-2 h-2 rounded-full bg-success inline-block" />正常</span>
      <span class="flex items-center gap-1"><i class="w-2 h-2 rounded-full bg-danger inline-block" />迟到/早退</span>
      <span class="flex items-center gap-1"><i class="w-2 h-2 rounded-full bg-muted-foreground inline-block" />缺勤</span>
      <span class="flex items-center gap-1"><i class="w-2 h-2 rounded-full bg-border inline-block" />休息</span>
      <span class="flex items-center gap-1"><i class="w-2 h-2 rounded-full bg-warning inline-block" />已补</span>
    </div>

    <!-- Header -->
    <div class="grid grid-cols-7 gap-1.5 mb-1.5">
      <div v-for="d in weekdays" :key="d" class="text-center text-[11px] font-semibold text-muted-foreground py-1">{{ d }}</div>
    </div>

    <!-- Grid -->
    <div class="grid grid-cols-7 gap-1.5">
      <div
        v-for="(cell, idx) in cells"
        :key="idx"
        :class="[
          'min-h-[84px] p-1.5 rounded-[var(--radius)] flex flex-col gap-0.5 transition-all',
          !cell.date ? 'bg-transparent' : [
            'bg-card border',
            cell.isToday ? 'border-primary bg-primary/5' : cell.isWeekend ? 'bg-subtle border-border' : 'border-border',
            !cell.record && !cell.isWeekend && cell.isPast ? 'bg-danger/5 border-danger/30' : '',
            cell.record?.isFixed ? 'border-l-[3px] border-l-warning' : '',
            cell.record ? 'cursor-pointer hover:border-primary/50 hover:shadow-sm' : '',
          ].join(' '),
        ]"
        @click="cell.record && $emit('select', cell.record)"
      >
        <template v-if="cell.date">
          <div class="flex items-center justify-between">
            <span class="text-[12px] font-semibold" :class="cell.isToday ? 'text-primary' : 'text-foreground'">{{ cell.day }}</span>
            <span v-if="cell.isToday" class="text-[9px] bg-primary text-primary-foreground px-1 rounded">今</span>
          </div>
          <div v-if="cell.record" class="flex flex-col gap-0.5">
            <div v-if="cell.record.checkInTime" class="flex items-center gap-1 text-[10px] text-muted-foreground font-mono">
              <i :class="['w-1.5 h-1.5 rounded-full shrink-0', dotColor(cell.record.checkInStatus as CheckStatus)]" />
              {{ shortTime(cell.record.checkInTime) }}
            </div>
            <div v-if="cell.record.checkOutTime" class="flex items-center gap-1 text-[10px] text-muted-foreground font-mono">
              <i :class="['w-1.5 h-1.5 rounded-full shrink-0', dotColor(cell.record.checkOutStatus as CheckStatus)]" />
              {{ shortTime(cell.record.checkOutTime) }}
            </div>
            <div v-if="cell.record.isFixed" class="text-[9px] text-warning mt-auto">已补卡</div>
          </div>
          <div v-else-if="cell.isWeekend" class="text-[10px] text-muted-foreground mt-auto">休息</div>
          <div v-else-if="cell.isPast" class="text-[10px] text-danger mt-auto">缺勤</div>
        </template>
      </div>
    </div>
  </div>
</template>
