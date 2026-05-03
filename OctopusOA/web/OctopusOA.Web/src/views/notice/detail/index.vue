<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ChevronLeft } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { getNoticeById } from '@/api/notice'
import type { NoticeItem } from '@/api/notice/types'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const notice = ref<NoticeItem | null>(null)

async function loadData() {
  loading.value = true
  try { notice.value = await getNoticeById(Number(route.params['id'])) }
  finally { loading.value = false }
}

type BadgeVariant = 'info' | 'warning' | 'danger' | 'default'
function typeVariant(t: string): BadgeVariant {
  return ({ '1': 'info', '2': 'warning', '3': 'danger' } as Record<string, BadgeVariant>)[t] ?? 'default'
}
function typeLabel(t: string) { return { '1': '通知', '2': '公告', '3': '紧急' }[t] ?? t }

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader title="公告详情">
      <template #actions>
        <button
          class="flex items-center gap-1 text-[12px] text-muted-foreground hover:text-foreground cursor-pointer border-0 bg-transparent"
          @click="router.push('/notice/list')"
        >
          <ChevronLeft :size="14" />返回列表
        </button>
      </template>
    </PageHeader>

    <div class="p-5 max-w-[860px]">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="notice" class="bg-card border border-border rounded-[var(--radius-lg)] p-6">
        <div class="flex items-center gap-3 mb-3">
          <Badge :variant="typeVariant(notice.noticeType)">{{ typeLabel(notice.noticeType) }}</Badge>
          <h1 class="text-[20px] font-semibold text-foreground">{{ notice.title }}</h1>
        </div>
        <div class="flex gap-5 text-[12px] text-muted-foreground mb-5">
          <span>发布人：{{ notice.publisher }}</span>
          <span>发布时间：{{ notice.publishTime }}</span>
          <span>来源：{{ notice.source === 'umc' ? 'UMC' : 'OA' }}</span>
        </div>
        <div class="border-t border-border pt-5 text-[14px] text-foreground leading-relaxed whitespace-pre-wrap">
          {{ notice.content }}
        </div>
      </div>
    </div>
  </div>
</template>
