<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { Users, ShoppingCart, Factory, CheckCircle } from 'lucide-vue-next'
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
    summary.value = {
      supplierCount: 0,
      purchaseInProgress: 0,
      workOrderInProgress: 0,
      workOrderCompleted: 0,
      totalPurchaseAmount: 0,
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
    label: '供应商总数',
    key: 'supplierCount' as const,
    icon: Users,
    color: 'text-[var(--primary-soft-fg)]',
    bg: 'bg-primary-soft',
    path: '/supplier',
  },
  {
    label: '进行中采购',
    key: 'purchaseInProgress' as const,
    icon: ShoppingCart,
    color: 'text-[var(--warning)]',
    bg: 'bg-[color-mix(in_oklch,var(--warning)_12%,transparent)]',
    path: '/purchase',
  },
  {
    label: '进行中工单',
    key: 'workOrderInProgress' as const,
    icon: Factory,
    color: 'text-[var(--info)]',
    bg: 'bg-[color-mix(in_oklch,var(--info)_12%,transparent)]',
    path: '/workorder',
  },
  {
    label: '已完成工单',
    key: 'workOrderCompleted' as const,
    icon: CheckCircle,
    color: 'text-[var(--success)]',
    bg: 'bg-[color-mix(in_oklch,var(--success)_12%,transparent)]',
    path: '/workorder',
  },
]
</script>

<template>
  <div>
    <PageHeader
      :title="`${greeting}，${userStore.userName || '用户'}`"
      sub="这是您的 MES 生产采购看板"
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
            {{ summary ? summary[card.key] : '—' }}
          </div>
        </div>
      </div>

      <!-- Quick links -->
      <div>
        <h2 class="text-[14px] font-semibold text-foreground mb-3">快捷入口</h2>
        <div class="flex flex-wrap gap-3">
          <button
            class="flex items-center gap-2 px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[13px] text-foreground hover:border-primary/40 hover:bg-[var(--subtle)] transition-colors cursor-pointer"
            @click="router.push('/purchase')"
          >
            <ShoppingCart :size="14" class="text-muted-foreground" />
            新建采购单
          </button>
          <button
            class="flex items-center gap-2 px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[13px] text-foreground hover:border-primary/40 hover:bg-[var(--subtle)] transition-colors cursor-pointer"
            @click="router.push('/workorder')"
          >
            <Factory :size="14" class="text-muted-foreground" />
            新建工单
          </button>
          <button
            class="flex items-center gap-2 px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[13px] text-foreground hover:border-primary/40 hover:bg-[var(--subtle)] transition-colors cursor-pointer"
            @click="router.push('/supplier')"
          >
            <Users :size="14" class="text-muted-foreground" />
            供应商管理
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
