<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Select, SelectTrigger, SelectContent, SelectItem, SelectValue } from '@/components/ui/select'
import { getNoticeList } from '@/api/notice'
import type { NoticeItem } from '@/api/notice/types'

const router = useRouter()
const loading = ref(false)
const list = ref<NoticeItem[]>([])
const typeFilter = ref('')
const statusFilter = ref<number | ''>('')

const filteredList = computed(() => {
  let arr = list.value
  if (statusFilter.value === 0) arr = arr.filter(n => !n.isRead)
  if (statusFilter.value === 1) arr = arr.filter(n => n.isRead)
  return arr
})

async function loadData() {
  loading.value = true
  try {
    list.value = (await getNoticeList(typeFilter.value ? { type: typeFilter.value } : undefined)).rows
  } finally { loading.value = false }
}

type BadgeVariant = 'info' | 'warning' | 'danger' | 'default'
function typeVariant(t: string): BadgeVariant {
  return ({ '1': 'info', '2': 'warning', '3': 'danger' } as Record<string, BadgeVariant>)[t] ?? 'default'
}
function typeLabel(t: string) { return { '1': '通知', '2': '公告', '3': '紧急' }[t] ?? t }
function stripContent(s: string) { return s.length > 100 ? s.slice(0, 100) + '...' : s }

const typeOptions = [
  { value: '', label: '全部类型' },
  { value: '1', label: '通知' },
  { value: '2', label: '公告' },
  { value: '3', label: '紧急' },
]
const statusOptions = [
  { value: '' as const, label: '全部状态' },
  { value: 0 as const, label: '未读' },
  { value: 1 as const, label: '已读' },
]

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader title="公告中心" />

    <div class="p-5">
      <!-- Filters -->
      <div class="flex items-center gap-3 mb-4">
        <Select :model-value="typeFilter" @update:model-value="typeFilter = String($event); loadData()">
          <SelectTrigger class="w-28"><SelectValue placeholder="全部类型" /></SelectTrigger>
          <SelectContent>
            <SelectItem v-for="o in typeOptions" :key="o.value" :value="o.value">{{ o.label }}</SelectItem>
          </SelectContent>
        </Select>
        <Select :model-value="String(statusFilter)" @update:model-value="statusFilter = $event === '' ? '' : Number($event) as 0 | 1">
          <SelectTrigger class="w-28"><SelectValue placeholder="全部状态" /></SelectTrigger>
          <SelectContent>
            <SelectItem v-for="o in statusOptions" :key="String(o.value)" :value="String(o.value)">{{ o.label }}</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!filteredList.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无公告</div>
      <div v-else class="space-y-3">
        <button
          v-for="n in filteredList"
          :key="n.noticeId"
          :class="[
            'w-full text-left bg-card border rounded-[var(--radius-lg)] p-4 cursor-pointer transition-all hover:border-primary/50 hover:shadow-sm',
            !n.isRead ? 'border-l-[3px] border-l-warning border-border' : 'border-border'
          ]"
          @click="router.push(`/notice/${n.noticeId}`)"
        >
          <div class="flex items-center gap-2 mb-1.5">
            <Badge :variant="typeVariant(n.noticeType)">{{ typeLabel(n.noticeType) }}</Badge>
            <span class="text-[14px] font-semibold text-foreground flex-1 text-left">{{ n.title }}</span>
            <div v-if="!n.isRead" class="w-2 h-2 rounded-full bg-danger shrink-0" />
          </div>
          <div class="flex gap-4 text-[11px] text-muted-foreground mb-1.5">
            <span>{{ n.publisher }}</span>
            <span class="font-mono-tnum">{{ n.publishTime }}</span>
          </div>
          <div class="text-[12px] text-muted-foreground line-clamp-2">{{ stripContent(n.content) }}</div>
        </button>
      </div>
    </div>
  </div>
</template>
