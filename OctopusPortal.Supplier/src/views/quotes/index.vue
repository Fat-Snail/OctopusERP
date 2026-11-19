<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { portalApi } from '@/api/portal'
import type { QuoteRequest, QuoteStatus } from '@/api/types'

const router = useRouter()
const quotes = ref<QuoteRequest[]>([])
const loading = ref(true)
const filter = ref<'all' | QuoteStatus>('all')

onMounted(async () => {
  quotes.value = await portalApi.getQuoteRequests()
  loading.value = false
})

const statusMeta: Record<QuoteStatus, { label: string; cls: string }> = {
  pending: { label: '待报价', cls: 'text-warning bg-warning/10' },
  responded: { label: '已报价', cls: 'text-primary-soft-fg bg-primary-soft' },
  closed: { label: '已关闭', cls: 'text-muted-foreground bg-muted' },
}

const quoteResultMeta: Record<string, { label: string; cls: string }> = {
  under_review: { label: '报价审核中', cls: 'text-info bg-info/10' },
  accepted: { label: '已中标', cls: 'text-success bg-success/10' },
  rejected: { label: '未中标', cls: 'text-danger bg-danger/10' },
}

const filtered = computed(() =>
  filter.value === 'all' ? quotes.value : quotes.value.filter((q) => q.status === filter.value)
)
</script>

<template>
  <div class="pb-4">
    <header class="sticky top-0 z-10 bg-background/90 backdrop-blur px-5 pt-6 pb-3 border-b border-border">
      <h1 class="text-lg font-semibold">询价响应</h1>
      <div class="flex gap-2 mt-3">
        <button
          v-for="f in [
            { key: 'all', label: '全部' },
            { key: 'pending', label: '待报价' },
            { key: 'responded', label: '已报价' },
          ]"
          :key="f.key"
          :class="[
            'h-7 px-3 rounded-full text-xs border-0 cursor-pointer transition-colors',
            filter === f.key ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground',
          ]"
          @click="filter = f.key as 'all' | QuoteStatus"
        >
          {{ f.label }}
        </button>
      </div>
    </header>

    <div v-if="loading" class="px-5 py-10 text-center text-muted-foreground text-sm">加载中…</div>

    <div v-else class="px-5 mt-3 space-y-3">
      <div
        v-for="q in filtered"
        :key="q.id"
        class="bg-card rounded-xl p-4 shadow-sm"
      >
        <div class="flex items-start justify-between gap-2">
          <div class="min-w-0">
            <p class="text-[11px] text-muted-foreground">{{ q.code }} · 截止 {{ q.deadline }}</p>
            <p class="text-sm font-medium mt-0.5 truncate">{{ q.productName }}</p>
          </div>
          <span :class="['text-[11px] px-2 py-0.5 rounded-full flex-none', statusMeta[q.status].cls]">
            {{ statusMeta[q.status].label }}
          </span>
        </div>

        <div class="flex items-center gap-4 mt-2.5 text-xs text-muted-foreground">
          <span>需求 {{ q.quantity.toLocaleString('zh-CN') }} {{ q.unit }}</span>
          <span>SKU {{ q.sku }}</span>
        </div>

        <!-- My quote -->
        <div v-if="q.myQuote" class="mt-3 rounded-lg bg-muted px-3 py-2.5">
          <div class="flex items-center justify-between text-xs">
            <span class="text-muted-foreground">我的报价</span>
            <span :class="['text-[10px] px-2 py-0.5 rounded-full', quoteResultMeta[q.myQuote.status]!.cls]">
              {{ quoteResultMeta[q.myQuote.status]!.label }}
            </span>
          </div>
          <div class="flex items-center gap-4 mt-1.5 text-sm">
            <span class="font-semibold text-primary">¥{{ q.myQuote.unitPrice }}/{{ q.unit }}</span>
            <span class="text-muted-foreground">交期 {{ q.myQuote.deliveryDays }} 天</span>
            <span class="text-muted-foreground text-[11px]">{{ q.myQuote.quotedAt }}</span>
          </div>
          <p v-if="q.myQuote.remark" class="text-[11px] text-muted-foreground mt-1">{{ q.myQuote.remark }}</p>
        </div>

        <button
          v-else
          class="mt-3 w-full h-9 rounded-lg bg-primary text-primary-foreground text-xs font-medium border-0 cursor-pointer active:opacity-80"
          @click="router.push(`/quotes/respond/${q.id}`)"
        >
          在线报价
        </button>
      </div>
    </div>
  </div>
</template>
