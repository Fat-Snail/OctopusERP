<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Plus, ChevronRight } from '@lucide/vue'
import { portalApi } from '@/api/portal'
import type { Inquiry, InquiryStatus } from '@/api/types'

const router = useRouter()
const inquiries = ref<Inquiry[]>([])
const loading = ref(true)
const filter = ref<'all' | InquiryStatus>('all')

onMounted(async () => {
  inquiries.value = await portalApi.getInquiries()
  loading.value = false
})

const statusMeta: Record<InquiryStatus, { label: string; cls: string }> = {
  submitted: { label: '已提交', cls: 'text-info bg-info/10' },
  quoting: { label: '报价中', cls: 'text-warning bg-warning/10' },
  quoted: { label: '已报价', cls: 'text-primary-soft-fg bg-primary-soft' },
  won: { label: '已成交', cls: 'text-success bg-success/10' },
}

const filtered = computed(() =>
  filter.value === 'all' ? inquiries.value : inquiries.value.filter((i) => i.status === filter.value)
)
</script>

<template>
  <div class="pb-4">
    <header class="sticky top-0 z-10 bg-background/90 backdrop-blur px-5 pt-6 pb-3 border-b border-border">
      <div class="flex items-center justify-between">
        <h1 class="text-lg font-semibold">我的询盘</h1>
        <button
          class="flex items-center gap-1 h-8 px-3 rounded-lg bg-primary text-primary-foreground text-xs border-0 cursor-pointer active:opacity-80"
          @click="router.push('/inquiries/new')"
        >
          <Plus :size="14" /> 新询盘
        </button>
      </div>
      <div class="flex gap-2 mt-3">
        <button
          v-for="f in [
            { key: 'all', label: '全部' },
            { key: 'submitted', label: '已提交' },
            { key: 'quoting', label: '报价中' },
            { key: 'quoted', label: '已报价' },
            { key: 'won', label: '已成交' },
          ]"
          :key="f.key"
          :class="[
            'h-7 px-3 rounded-full text-xs border-0 cursor-pointer transition-colors',
            filter === f.key ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground',
          ]"
          @click="filter = f.key as 'all' | InquiryStatus"
        >
          {{ f.label }}
        </button>
      </div>
    </header>

    <div v-if="loading" class="px-5 py-10 text-center text-muted-foreground text-sm">加载中…</div>

    <div v-else class="px-5 mt-3 space-y-3">
      <div
        v-for="inq in filtered"
        :key="inq.id"
        class="bg-card rounded-xl p-4 shadow-sm"
      >
        <div class="flex items-start justify-between gap-2">
          <div class="min-w-0">
            <p class="text-[11px] text-muted-foreground">{{ inq.code }}</p>
            <p class="text-sm font-medium mt-0.5 truncate">{{ inq.productName }}</p>
          </div>
          <span :class="['text-[11px] px-2 py-0.5 rounded-full flex-none', statusMeta[inq.status].cls]">
            {{ statusMeta[inq.status].label }}
          </span>
        </div>

        <div class="flex items-center gap-4 mt-2.5 text-xs text-muted-foreground">
          <span>{{ inq.quantity.toLocaleString('zh-CN') }} {{ inq.unit }}</span>
          <span v-if="inq.quotedAmount">报价 ¥{{ inq.quotedAmount.toLocaleString('zh-CN') }}</span>
          <span v-if="inq.quotedDeliveryDays">交期 {{ inq.quotedDeliveryDays }} 天</span>
        </div>

        <!-- Timeline -->
        <div class="flex items-center mt-3 gap-0">
          <template v-for="(step, idx) in inq.timeline" :key="idx">
            <div class="flex flex-col items-center flex-none">
              <span
                :class="[
                  'w-2 h-2 rounded-full',
                  step.done ? 'bg-primary' : 'bg-border',
                ]"
              />
              <span :class="['text-[9px] mt-1 whitespace-nowrap', step.done ? 'text-primary' : 'text-muted-foreground']">
                {{ step.label }}
              </span>
            </div>
            <div v-if="idx < inq.timeline.length - 1" :class="['h-px flex-1 mx-1', step.done ? 'bg-primary/60' : 'bg-border']" />
          </template>
        </div>
      </div>

      <button
        class="w-full bg-card rounded-xl p-4 flex items-center justify-between border-0 cursor-pointer shadow-sm active:opacity-80"
        @click="router.push('/inquiries/new')"
      >
        <span class="text-sm text-muted-foreground">没有找到合适的？提交新询盘</span>
        <ChevronRight :size="16" class="text-muted-foreground" />
      </button>
    </div>
  </div>
</template>
