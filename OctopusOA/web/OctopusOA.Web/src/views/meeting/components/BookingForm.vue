<script setup lang="ts">
import { ref, reactive, onMounted, watch } from 'vue'
import type { MeetingRoom, CreateBookingRequest } from '@/api/meeting/types'
import { getRoomList } from '@/api/meeting'
import { getUsers } from '@/api/contact'
import type { ContactUser } from '@/api/contact/types'
import { Select, SelectTrigger, SelectContent, SelectItem, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'

const props = defineProps<{
  defaultRoomId?: number
  defaultStart?: string
  defaultEnd?: string
}>()

const rooms = ref<MeetingRoom[]>([])
const users = ref<ContactUser[]>([])
const errors = reactive<Record<string, string>>({})

const form = reactive<{ roomId: number; title: string; startTime: string; endTime: string; description: string; attendees: number[] }>({
  roomId: 0, title: '', startTime: '', endTime: '', description: '', attendees: [],
})

function validate(): boolean {
  errors['roomId'] = form.roomId ? '' : '请选择会议室'
  errors['title'] = form.title.trim() ? '' : '请输入会议主题'
  errors['startTime'] = form.startTime ? '' : '请选择开始时间'
  errors['endTime'] = form.endTime ? '' : '请选择结束时间'
  return !Object.values(errors).some(Boolean)
}

function toPayload(): CreateBookingRequest | null {
  if (!form.startTime || !form.endTime) return null
  return {
    roomId: form.roomId,
    title: form.title,
    startTime: form.startTime,
    endTime: form.endTime,
    description: form.description || undefined,
    attendees: form.attendees,
  }
}

defineExpose({ validate, toPayload })

watch(() => props.defaultRoomId, v => { if (v) form.roomId = v }, { immediate: true })
watch(() => [props.defaultStart, props.defaultEnd], ([s, e]) => {
  if (s) form.startTime = (s as string).slice(0, 16)
  if (e) form.endTime = (e as string).slice(0, 16)
}, { immediate: true })

onMounted(async () => {
  rooms.value = (await getRoomList()).filter(r => r.status === 1)
  users.value = (await getUsers({})).rows
})
</script>

<template>
  <div class="space-y-3">
    <div>
      <label class="block text-[12px] font-medium mb-1.5"><span class="text-danger">*</span> 会议室</label>
      <Select :model-value="form.roomId ? String(form.roomId) : ''" @update:model-value="form.roomId = Number($event)">
        <SelectTrigger :class="cn('w-full', errors['roomId'] ? 'border-danger' : '')">
          <SelectValue placeholder="选择会议室" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem v-for="r in rooms" :key="r.id" :value="String(r.id)">
            {{ r.name }}（容纳 {{ r.capacity }} 人{{ r.location ? ' · ' + r.location : '' }}）
          </SelectItem>
        </SelectContent>
      </Select>
      <p v-if="errors['roomId']" class="text-[11px] text-danger mt-0.5">{{ errors['roomId'] }}</p>
    </div>

    <div>
      <label class="block text-[12px] font-medium mb-1.5"><span class="text-danger">*</span> 会议主题</label>
      <input v-model="form.title" type="text" placeholder="会议主题" :class="['w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring', errors['title'] ? 'border-danger' : 'border-input']" />
      <p v-if="errors['title']" class="text-[11px] text-danger mt-0.5">{{ errors['title'] }}</p>
    </div>

    <div class="grid grid-cols-2 gap-3">
      <div>
        <label class="block text-[12px] font-medium mb-1.5"><span class="text-danger">*</span> 开始时间</label>
        <input v-model="form.startTime" type="datetime-local" :class="['w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring', errors['startTime'] ? 'border-danger' : 'border-input']" />
        <p v-if="errors['startTime']" class="text-[11px] text-danger mt-0.5">{{ errors['startTime'] }}</p>
      </div>
      <div>
        <label class="block text-[12px] font-medium mb-1.5"><span class="text-danger">*</span> 结束时间</label>
        <input v-model="form.endTime" type="datetime-local" :class="['w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring', errors['endTime'] ? 'border-danger' : 'border-input']" />
        <p v-if="errors['endTime']" class="text-[11px] text-danger mt-0.5">{{ errors['endTime'] }}</p>
      </div>
    </div>

    <div>
      <label class="block text-[12px] font-medium mb-1.5">参会人</label>
      <select v-model="form.attendees" multiple class="w-full px-3 py-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring h-24">
        <option v-for="u in users" :key="u.umcUserId" :value="u.umcUserId">{{ u.nickName }}（{{ u.userName }}）</option>
      </select>
      <p class="text-[11px] text-muted-foreground mt-0.5">按住 Ctrl/Cmd 多选</p>
    </div>

    <div>
      <label class="block text-[12px] font-medium mb-1.5">会议说明</label>
      <textarea v-model="form.description" rows="2" placeholder="会议说明（可选）" class="w-full px-3 py-2 border border-input rounded-[var(--radius)] bg-card text-[12px] resize-none focus:outline-none focus:ring-2 focus:ring-ring" />
    </div>
  </div>
</template>
