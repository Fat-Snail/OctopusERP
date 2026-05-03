<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { ChevronLeft, ChevronRight, Plus } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'

import { toast } from '@/components/ui/toast'
import { getRoomList, getAllCalendar, book } from '@/api/meeting'
import type { MeetingRoom, MeetingBooking } from '@/api/meeting/types'
import BookingForm from '../components/BookingForm.vue'

const rooms = ref<MeetingRoom[]>([])
const bookings = ref<MeetingBooking[]>([])
const loading = ref(false)
const anchor = ref(new Date())
const formVisible = ref(false)
const submitting = ref(false)
const detailVisible = ref(false)
const selected = ref<MeetingBooking | null>(null)
const bookingFormRef = ref<{ validate: () => boolean; toPayload: () => ReturnType<typeof nullPayload> }>()
const defaultRoomId = ref<number>(0)
const defaultStart = ref<string>('')
const defaultEnd = ref<string>('')

function nullPayload() { return null }

interface DayInfo { iso: string; label: string; weekday: string; isToday: boolean }

const days = computed<DayInfo[]>(() => {
  const list: DayInfo[] = []
  const start = startOfWeek(anchor.value)
  const todayIso = toISO(new Date())
  const wdNames = ['日', '一', '二', '三', '四', '五', '六']
  for (let i = 0; i < 7; i++) {
    const d = new Date(start)
    d.setDate(d.getDate() + i)
    const iso = toISO(d)
    list.push({ iso, label: `${d.getMonth() + 1}-${String(d.getDate()).padStart(2, '0')}`, weekday: '周' + wdNames[d.getDay()], isToday: iso === todayIso })
  }
  return list
})

const rangeLabel = computed(() => days.value.length ? `${days.value[0].iso} ~ ${days.value[6].iso}` : '')

function startOfWeek(d: Date): Date {
  const copy = new Date(d); copy.setHours(0, 0, 0, 0)
  const dow = (copy.getDay() + 6) % 7; copy.setDate(copy.getDate() - dow); return copy
}

function toISO(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function shiftWeek(delta: number) {
  const d = new Date(anchor.value); d.setDate(d.getDate() + 7 * delta); anchor.value = d; loadCalendar()
}

function goThisWeek() { anchor.value = new Date(); loadCalendar() }

async function loadRooms() { rooms.value = (await getRoomList()).filter(r => r.status === 1) }

async function loadCalendar() {
  loading.value = true
  try { bookings.value = await getAllCalendar(days.value[0].iso, 'week') }
  finally { loading.value = false }
}

function bookingsFor(roomId: number, iso: string): MeetingBooking[] {
  return bookings.value.filter(b => b.roomId === roomId && b.startTime.slice(0, 10) === iso)
}

function formatTime(iso: string): string { return iso.length >= 16 ? iso.slice(11, 16) : iso }

function openNew() {
  defaultRoomId.value = rooms.value[0]?.id ?? 0; defaultStart.value = ''; defaultEnd.value = ''; formVisible.value = true
}

function openNewForSlot(roomId: number, iso: string) {
  defaultRoomId.value = roomId
  defaultStart.value = `${iso}T09:00`
  defaultEnd.value = `${iso}T10:00`
  formVisible.value = true
}

async function handleSubmit() {
  if (!bookingFormRef.value) return
  const valid = bookingFormRef.value.validate()
  if (!valid) return
  const payload = bookingFormRef.value.toPayload()
  if (!payload) return
  submitting.value = true
  try {
    await book(payload); toast('预订成功', 'success'); formVisible.value = false; await loadCalendar()
  } catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

function openDetail(b: MeetingBooking) { selected.value = b; detailVisible.value = true }

onMounted(async () => { await loadRooms(); await loadCalendar() })
</script>

<template>
  <div>
    <PageHeader title="会议室预订">
      <template #actions>
        <button class="h-[var(--row-h)] px-3 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 flex items-center gap-1" @click="openNew">
          <Plus :size="13" />新建预订
        </button>
      </template>
    </PageHeader>

    <div class="p-5">
      <div class="flex items-center gap-2 mb-4">
        <button class="p-1.5 border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="shiftWeek(-1)"><ChevronLeft :size="14" /></button>
        <span class="text-[12px] font-semibold text-foreground min-w-[200px] text-center">{{ rangeLabel }}</span>
        <button class="p-1.5 border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="shiftWeek(1)"><ChevronRight :size="14" /></button>
        <button class="h-[var(--row-h)] px-3 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="goThisWeek">本周</button>
      </div>

      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!rooms.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无会议室</div>
      <div v-else class="overflow-x-auto border border-border rounded-[var(--radius-lg)]">
        <table class="w-full min-w-[900px] text-[12px] border-collapse">
          <thead>
            <tr class="bg-subtle">
              <th class="px-3 py-2 text-left font-semibold text-muted-foreground border-b border-border w-[140px]">会议室</th>
              <th
                v-for="d in days"
                :key="d.iso"
                :class="['px-3 py-2 text-center font-semibold border-b border-l border-border', d.isToday ? 'text-primary bg-primary/5' : 'text-foreground']"
              >
                <div>{{ d.label }}</div>
                <div class="text-[10px] font-normal text-muted-foreground mt-0.5">{{ d.weekday }}</div>
              </th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="room in rooms" :key="room.id" class="hover:bg-subtle/50">
              <td class="px-3 py-2 border-r border-border bg-subtle/50">
                <div class="font-semibold text-foreground">{{ room.name }}</div>
                <div class="text-[10px] text-muted-foreground mt-0.5">{{ room.capacity }} 人 · {{ room.location }}</div>
              </td>
              <td
                v-for="d in days"
                :key="d.iso"
                class="px-2 py-2 border-l border-border min-w-[120px] align-top cursor-pointer hover:bg-primary/5 transition-colors"
                @click="openNewForSlot(room.id, d.iso)"
              >
                <div
                  v-for="b in bookingsFor(room.id, d.iso)"
                  :key="b.id"
                  class="bg-primary text-primary-foreground px-2 py-1 rounded-[var(--radius)] mb-1 cursor-pointer hover:brightness-110 transition-all"
                  @click.stop="openDetail(b)"
                >
                  <div class="font-mono text-[10px] font-semibold">{{ formatTime(b.startTime) }}–{{ formatTime(b.endTime) }}</div>
                  <div class="font-semibold text-[11px]">{{ b.title }}</div>
                  <div class="text-[10px] opacity-85">{{ b.userName }}</div>
                </div>
                <div v-if="!bookingsFor(room.id, d.iso).length" class="text-[10px] text-muted-foreground text-center py-1">+ 点击预订</div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Booking form modal -->
    <Dialog v-model:open="formVisible">
      <DialogContent class="p-5 w-[520px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">新建预订</DialogTitle>
        </DialogHeader>
        <BookingForm ref="bookingFormRef" :default-room-id="defaultRoomId" :default-start="defaultStart" :default-end="defaultEnd" />
        <DialogFooter>
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="formVisible = false">取消</button>
          <button :disabled="submitting" class="px-4 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50" @click="handleSubmit">
            {{ submitting ? '预订中...' : '预订' }}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Detail modal -->
    <Dialog v-model:open="detailVisible">
      <DialogContent class="p-5 w-[460px]">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">预订详情</DialogTitle>
        </DialogHeader>
        <div v-if="selected" class="space-y-2 text-[12px]">
          <div class="flex justify-between py-1.5 border-b border-border"><span class="text-muted-foreground">会议室</span><span>{{ selected.roomName }}</span></div>
          <div class="flex justify-between py-1.5 border-b border-border"><span class="text-muted-foreground">主题</span><span class="font-medium">{{ selected.title }}</span></div>
          <div class="flex justify-between py-1.5 border-b border-border"><span class="text-muted-foreground">预订人</span><span>{{ selected.userName }}</span></div>
          <div class="flex justify-between py-1.5 border-b border-border"><span class="text-muted-foreground">开始时间</span><span class="font-mono-tnum">{{ selected.startTime }}</span></div>
          <div class="flex justify-between py-1.5 border-b border-border"><span class="text-muted-foreground">结束时间</span><span class="font-mono-tnum">{{ selected.endTime }}</span></div>
          <div class="flex justify-between py-1.5 border-b border-border items-start">
            <span class="text-muted-foreground">参会人</span>
            <div class="flex flex-wrap gap-1 justify-end">
              <Badge v-for="n in selected.attendeeNames" :key="n" variant="secondary">{{ n }}</Badge>
              <span v-if="!selected.attendeeNames.length" class="text-muted-foreground">无</span>
            </div>
          </div>
          <div class="flex justify-between py-1.5 border-b border-border"><span class="text-muted-foreground">说明</span><span>{{ selected.description || '-' }}</span></div>
          <div class="flex justify-between py-1.5">
            <span class="text-muted-foreground">状态</span>
            <Badge :variant="selected.status === 'confirmed' ? 'success' : 'secondary'">{{ selected.status === 'confirmed' ? '已确认' : '已取消' }}</Badge>
          </div>
        </div>
        <div class="flex justify-end mt-4">
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="detailVisible = false">关闭</button>
        </div>
      </DialogContent>
    </Dialog>
  </div>
</template>
