<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { Users, MessageSquare, FileText, ScrollText } from 'lucide-vue-next'
import { useUserStore } from '@/store/modules/user'
import { getSummary } from '@/api/stats'
import type { StatsSummaryResponse } from '@/api/stats/types'
import PageHeader from '@/components/app/PageHeader.vue'

const router = useRouter()
const userStore = useUserStore()

const summary = ref<StatsSummaryResponse | null>(null)
const loading = ref(false)

onMounted(async () => {
  loading.value = true
  try {
    summary.value = await getSummary()
  } catch {
    // use placeholder data when backend not available
    summary.value = {
      totalCustomers: 0,
      openInquiries: 0,
      activeContracts: 0,
      totalContractAmount: 0,
      currency: 'CNY',
    }
  } finally {
    loading.value = false
  }
})

const greeting = computed(() => {
  const h = new Date().getHours()
  if (h < 12) return '早上好'
  if (h < 18) return '下午好'
  return '晚上好'
})

const statCards = [
  {
    label: '客户总数',
    key: 'totalCustomers' as const,
    icon: Users,
    color: 'text-[var(--primary-soft-fg)]',
    bg: 'bg-primary-soft',
    path: '/customer',
  },
  {
    label: '进行中询盘',
    key: 'openInquiries' as const,
    icon: MessageSquare,
    color: 'text-[var(--warning)]',
    bg: 'bg-[color-mix(in_oklch,var(--warning)_12%,transparent)]',
    path: '/inquiry',
  },
  {
    label: '活跃合同',
    key: 'activeContracts' as const,
    icon: ScrollText,
    color: 'text-[var(--success)]',
    bg: 'bg-[color-mix(in_oklch,var(--success)_12%,transparent)]',
    path: '/contract',
  },
  {
    label: '合同总金额',
    key: 'totalContractAmount' as const,
    icon: FileText,
    color: 'text-[var(--info)]',
    bg: 'bg-[color-mix(in_oklch,var(--info)_12%,transparent)]',
    path: '/contract',
    isAmount: true,
  },
]

function formatValue(card: (typeof statCards)[0], val: number): string {
  if (card.isAmount) {
    return new Intl.NumberFormat('zh-CN', {
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(val)
  }
  return String(val)
}
</script>

<template>
  <div>
    <PageHeader
      :title="`${greeting}，${userStore.userName || '用户'}`"
      sub="这是您的 CRM 工作台概览"
    />

    <div class="p-5 space-y-6">
    <!-- Stat cards -->
    <div class="grid grid-cols-4 gap-4">
      <div
        v-for="card in statCards"
        :key="card.key"
        class="bg-card border border-border rounded-[var(--radius-lg)] p-4 cursor-pointer hover:border-primary/40 transition-colors"
        @click="router.push(card.path)"
      >
        <div class="flex items-start justify-between mb-3">
          <span class="text-[12px] text-muted-foreground">{{ card.label }}</span>
          <div :class="[card.bg, 'w-8 h-8 rounded-lg flex items-center justify-center']">
            <component :is="card.icon" :size="16" :class="card.color" />
          </div>
        </div>
        <div v-if="loading" class="h-7 w-16 bg-muted rounded animate-pulse" />
        <div v-else class="text-[22px] font-semibold text-foreground font-mono-tnum">
          {{ summary ? formatValue(card, summary[card.key]) : '—' }}
        </div>
      </div>
    </div>

    <!-- Quick links -->
    <div>
      <h2 class="text-[14px] font-semibold text-foreground mb-3">快捷入口</h2>
      <div class="flex flex-wrap gap-3">
        <button
          class="flex items-center gap-2 px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[13px] text-foreground hover:border-primary/40 hover:bg-[var(--subtle)] transition-colors cursor-pointer"
          @click="router.push('/customer')"
        >
          <Users :size="14" class="text-muted-foreground" />
          新建客户
        </button>
        <button
          class="flex items-center gap-2 px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[13px] text-foreground hover:border-primary/40 hover:bg-[var(--subtle)] transition-colors cursor-pointer"
          @click="router.push('/inquiry')"
        >
          <MessageSquare :size="14" class="text-muted-foreground" />
          录入询盘
        </button>
        <button
          class="flex items-center gap-2 px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[13px] text-foreground hover:border-primary/40 hover:bg-[var(--subtle)] transition-colors cursor-pointer"
          @click="router.push('/quote')"
        >
          <FileText :size="14" class="text-muted-foreground" />
          查看报价
        </button>
        <button
          class="flex items-center gap-2 px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[13px] text-foreground hover:border-primary/40 hover:bg-[var(--subtle)] transition-colors cursor-pointer"
          @click="router.push('/contract')"
        >
          <ScrollText :size="14" class="text-muted-foreground" />
          合同管理
        </button>
      </div>
    </div>
    </div>
  </div>
</template>
