<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft } from '@lucide/vue'
import { getNoticeById } from '@/api/notice'
import type { NoticeItem } from '@/api/notice/types'

const route = useRoute()
const router = useRouter()
const noticeId = Number(route.params['id'])
const notice = ref<NoticeItem | null>(null)
const loading = ref(true)

onMounted(async () => {
  loading.value = true
  try {
    notice.value = await getNoticeById(noticeId)
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="min-h-full bg-subtle">
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4 gap-3">
        <button
          class="w-8 h-8 flex items-center justify-center rounded-full hover:bg-muted border-0 bg-transparent cursor-pointer"
          @click="router.back()"
        >
          <ArrowLeft :size="20" class="text-foreground" />
        </button>
        <h1 class="text-[16px] font-semibold text-foreground flex-1 truncate">
          {{ notice?.title || '公告详情' }}
        </h1>
      </div>
    </div>

    <div v-if="loading" class="py-16 text-center text-muted-foreground text-[12px]">加载中...</div>
    <div v-else-if="notice" class="p-4">
      <div class="bg-background border border-border rounded-xl p-5">
        <h2 class="text-[18px] font-semibold text-foreground leading-snug mb-3">{{ notice.title }}</h2>
        <div class="flex items-center gap-3 pb-3 mb-4 border-b border-border">
          <span class="text-[12px] text-muted-foreground">{{ notice.publisher }}</span>
          <span class="text-[12px] text-muted-foreground">{{ notice.publishTime }}</span>
        </div>
        <div class="text-[14px] text-foreground leading-relaxed whitespace-pre-wrap">{{ notice.content }}</div>
      </div>
    </div>
  </div>
</template>
