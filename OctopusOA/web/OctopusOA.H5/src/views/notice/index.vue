<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Bell, AlertCircle, Megaphone, ArrowLeft } from '@lucide/vue'
import { getNoticeList } from '@/api/notice'
import type { NoticeItem } from '@/api/notice/types'

const router = useRouter()
const loading = ref(true)
const list = ref<NoticeItem[]>([])

const typeLabel: Record<string, string> = { '1': '通知', '2': '公告', '3': '紧急' }
const typeIconMap = { '1': Bell, '2': Megaphone, '3': AlertCircle }
const typeClass: Record<string, string> = {
  '1': 'bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]',
  '2': 'bg-[color-mix(in_oklch,var(--primary)_14%,transparent)] text-primary',
  '3': 'bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]',
}

onMounted(async () => {
  loading.value = true
  try {
    list.value = (await getNoticeList()).rows
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="min-h-full">
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4 gap-2">
        <button
          class="w-8 h-8 flex items-center justify-center rounded-full hover:bg-muted border-0 bg-transparent cursor-pointer shrink-0"
          @click="router.push('/home')"
        >
          <ArrowLeft :size="20" class="text-foreground" />
        </button>
        <h1 class="text-[17px] font-semibold text-foreground">公告通知</h1>
      </div>
    </div>

    <div class="p-4">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!list.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无公告</div>
      <div v-else class="space-y-3">
        <div
          v-for="item in list"
          :key="item.noticeId"
          class="bg-background border border-border rounded-xl p-4 cursor-pointer hover:bg-muted/30 transition-colors"
          :class="!item.isRead ? 'border-primary/30' : ''"
          @click="router.push(`/notice/${item.noticeId}`)"
        >
          <div class="flex items-start gap-3">
            <div :class="['w-9 h-9 rounded-lg flex items-center justify-center shrink-0', typeClass[item.noticeType]]">
              <component :is="typeIconMap[item.noticeType]" :size="16" />
            </div>
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 mb-1">
                <p class="text-[14px] font-medium text-foreground flex-1 truncate">{{ item.title }}</p>
                <span v-if="!item.isRead" class="w-2 h-2 rounded-full bg-primary shrink-0" />
              </div>
              <div class="flex items-center gap-2">
                <span :class="['text-[11px] font-medium px-1.5 py-0.5 rounded-full', typeClass[item.noticeType]]">
                  {{ typeLabel[item.noticeType] }}
                </span>
                <span class="text-[11px] text-muted-foreground">{{ item.publisher }}</span>
                <span class="text-[11px] text-muted-foreground ml-auto">{{ item.publishTime }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
