<script setup lang="ts">
import { ref, onMounted } from 'vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { toast, confirm } from '@/components/ui/toast'
import { getMineBookings, cancelBooking } from '@/api/meeting'
import type { MeetingBooking } from '@/api/meeting/types'

const loading = ref(false)
const list = ref<MeetingBooking[]>([])

async function load() {
  loading.value = true
  try { list.value = (await getMineBookings()).rows } finally { loading.value = false }
}

function canCancel(b: MeetingBooking): boolean {
  if (b.status !== 'confirmed') return false
  const start = new Date(b.startTime.replace(' ', 'T'))
  return start.getTime() > Date.now() - 8 * 3600 * 1000
}

async function handleCancel(b: MeetingBooking) {
  try {
    await confirm(`确认取消预订「${b.title}」？`)
    await cancelBooking(b.id); toast('已取消', 'success'); await load()
  } catch { /* cancelled */ }
}

onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="我的预订" sub="查看你发起的会议室预订" />

    <div class="p-5">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!list.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无预订记录</div>
      <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">主题</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">会议室</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">开始时间</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">结束时间</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">参会人</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">状态</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">操作</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in list" :key="row.id" class="hover:bg-subtle">
              <td class="px-3 py-2 font-medium truncate max-w-[180px]">{{ row.title }}</td>
              <td class="px-3 py-2 text-muted-foreground">{{ row.roomName }}</td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground whitespace-nowrap">{{ row.startTime }}</td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground whitespace-nowrap">{{ row.endTime }}</td>
              <td class="px-3 py-2">
                <div class="flex flex-wrap gap-1">
                  <Badge v-for="n in row.attendeeNames" :key="n" variant="secondary">{{ n }}</Badge>
                  <span v-if="!row.attendeeNames.length" class="text-muted-foreground">-</span>
                </div>
              </td>
              <td class="px-3 py-2">
                <Badge :variant="row.status === 'confirmed' ? 'success' : 'secondary'">
                  {{ row.status === 'confirmed' ? '已确认' : '已取消' }}
                </Badge>
              </td>
              <td class="px-3 py-2 whitespace-nowrap">
                <button v-if="canCancel(row)" class="text-[11px] text-danger hover:underline cursor-pointer border-0 bg-transparent" @click="handleCancel(row)">取消预订</button>
                <span v-else class="text-muted-foreground">-</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
