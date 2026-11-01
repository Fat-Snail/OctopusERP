<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft, Plus, Calendar, MapPin, Users, Clock } from '@lucide/vue'
import { getMineBookings, cancelBooking, getRoomList, book } from '@/api/meeting'
import type { MeetingBooking, MeetingRoom } from '@/api/meeting/types'

const router = useRouter()
const myBookings = ref<MeetingBooking[]>([])
const loading = ref(true)
const showNewForm = ref(false)
const rooms = ref<MeetingRoom[]>([])
const submitting = ref(false)
const formError = ref('')

const newBooking = ref({
  roomId: 0,
  title: '',
  startTime: '',
  endTime: '',
  description: '',
})

function formatDateTime(dt: string) {
  if (!dt) return ''
  return dt.replace('T', ' ').slice(0, 16)
}

async function loadData() {
  loading.value = true
  try {
    myBookings.value = (await getMineBookings()).rows
  } finally {
    loading.value = false
  }
}

async function handleCancel(id: number) {
  if (!confirm('确认取消此预订？')) return
  try {
    await cancelBooking(id)
    await loadData()
  } catch (e: unknown) {
    alert((e as Error).message)
  }
}

async function handleBook() {
  const { roomId, title, startTime, endTime } = newBooking.value
  if (!roomId || !title || !startTime || !endTime) { formError.value = '请填写所有必填项'; return }
  if (new Date(endTime) <= new Date(startTime)) { formError.value = '结束时间须晚于开始时间'; return }
  submitting.value = true
  formError.value = ''
  try {
    await book({
      roomId,
      title,
      startTime: new Date(startTime).toISOString(),
      endTime: new Date(endTime).toISOString(),
      description: newBooking.value.description,
      attendees: [],
    })
    showNewForm.value = false
    newBooking.value = { roomId: 0, title: '', startTime: '', endTime: '', description: '' }
    await loadData()
  } catch (e: unknown) {
    formError.value = (e as Error).message || '预订失败'
  } finally {
    submitting.value = false
  }
}

onMounted(async () => {
  rooms.value = await getRoomList()
  await loadData()
})
</script>

<template>
  <div class="min-h-full">
    <!-- Header -->
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4 gap-3">
        <button class="w-8 h-8 flex items-center justify-center rounded-full hover:bg-muted border-0 bg-transparent cursor-pointer" @click="router.push('/h5/home')">
          <ArrowLeft :size="20" class="text-foreground" />
        </button>
        <h1 class="text-[16px] font-semibold text-foreground flex-1">我的会议</h1>
        <button
          class="flex items-center gap-1 text-primary text-[13px] border-0 bg-transparent cursor-pointer"
          @click="showNewForm = true"
        >
          <Plus :size="16" /> 预订
        </button>
      </div>
    </div>

    <div class="p-4">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!myBookings.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无预订记录</div>
      <div v-else class="space-y-3">
        <div
          v-for="booking in myBookings"
          :key="booking.id"
          class="bg-background border border-border rounded-xl p-4"
          :class="booking.status === 'cancelled' ? 'opacity-60' : ''"
        >
          <div class="flex items-start justify-between gap-2 mb-3">
            <h3 class="text-[14px] font-semibold text-foreground flex-1">{{ booking.title }}</h3>
            <span :class="['text-[11px] font-medium px-2 py-0.5 rounded-full shrink-0', booking.status === 'confirmed' ? 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]' : 'bg-muted text-muted-foreground']">
              {{ booking.status === 'confirmed' ? '已确认' : '已取消' }}
            </span>
          </div>
          <div class="space-y-1.5">
            <div class="flex items-center gap-2 text-[12px] text-muted-foreground">
              <MapPin :size="13" class="shrink-0" />
              <span>{{ booking.roomName }}</span>
            </div>
            <div class="flex items-center gap-2 text-[12px] text-muted-foreground">
              <Clock :size="13" class="shrink-0" />
              <span>{{ formatDateTime(booking.startTime) }} — {{ formatDateTime(booking.endTime) }}</span>
            </div>
            <div v-if="booking.attendeeNames?.length" class="flex items-center gap-2 text-[12px] text-muted-foreground">
              <Users :size="13" class="shrink-0" />
              <span class="truncate">{{ booking.attendeeNames.join(', ') }}</span>
            </div>
          </div>
          <div v-if="booking.status === 'confirmed'" class="mt-3 pt-3 border-t border-border">
            <button
              class="text-[12px] text-danger cursor-pointer border-0 bg-transparent"
              @click="handleCancel(booking.id)"
            >
              取消预订
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- New booking sheet -->
    <Teleport to="body">
      <div v-if="showNewForm" class="fixed inset-0 z-50">
        <div class="absolute inset-0 bg-black/40" @click="showNewForm = false" />
        <div class="absolute bottom-0 left-0 right-0 bg-background rounded-t-2xl p-5 pb-8 max-h-[90vh] overflow-y-auto">
          <h3 class="text-[16px] font-semibold text-foreground mb-5">预订会议室</h3>

          <div class="space-y-4">
            <div>
              <label class="block text-[12px] font-medium text-foreground mb-1.5">会议室 <span class="text-danger">*</span></label>
              <select
                v-model="newBooking.roomId"
                class="w-full h-11 px-3 rounded-[var(--radius)] border border-input bg-background text-[14px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              >
                <option value="0" disabled>请选择会议室</option>
                <option v-for="room in rooms.filter(r => r.status === 1)" :key="room.id" :value="room.id">
                  {{ room.name }} ({{ room.location || '—' }}, {{ room.capacity }}人)
                </option>
              </select>
            </div>

            <div>
              <label class="block text-[12px] font-medium text-foreground mb-1.5">会议主题 <span class="text-danger">*</span></label>
              <input
                v-model="newBooking.title"
                placeholder="请输入会议主题"
                class="w-full h-11 px-3 rounded-[var(--radius)] border border-input bg-background text-[14px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              />
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-[12px] font-medium text-foreground mb-1.5">开始时间 <span class="text-danger">*</span></label>
                <input
                  v-model="newBooking.startTime"
                  type="datetime-local"
                  class="w-full h-11 px-3 rounded-[var(--radius)] border border-input bg-background text-[13px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                />
              </div>
              <div>
                <label class="block text-[12px] font-medium text-foreground mb-1.5">结束时间 <span class="text-danger">*</span></label>
                <input
                  v-model="newBooking.endTime"
                  type="datetime-local"
                  class="w-full h-11 px-3 rounded-[var(--radius)] border border-input bg-background text-[13px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                />
              </div>
            </div>

            <div>
              <label class="block text-[12px] font-medium text-foreground mb-1.5">备注</label>
              <textarea
                v-model="newBooking.description"
                placeholder="会议说明（可选）"
                class="w-full h-20 px-3 py-2 rounded-[var(--radius)] border border-input bg-background text-[14px] text-foreground resize-none focus:outline-none focus:ring-2 focus:ring-ring"
              />
            </div>
          </div>

          <p v-if="formError" class="text-[12px] text-danger mt-3">{{ formError }}</p>
          <button
            :disabled="submitting"
            class="w-full h-12 bg-primary text-primary-foreground text-[15px] font-medium rounded-[var(--radius)] hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border-0 mt-4"
            @click="handleBook"
          >
            {{ submitting ? '预订中...' : '确认预订' }}
          </button>
        </div>
      </div>
    </Teleport>
  </div>
</template>
