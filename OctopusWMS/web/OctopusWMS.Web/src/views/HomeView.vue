<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { Warehouse, PackagePlus, PackageMinus, AlertTriangle } from 'lucide-vue-next'
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
      warehouseCount: 0,
      inboundToday: 0,
      outboundPending: 0,
      lowStockCount: 0,
      totalInventoryItems: 0,
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
    label: '仓库总数',
    key: 'warehouseCount' as const,
    icon: Warehouse,
    color: 'text-[var(--primary-soft-fg)]',
    bg: 'bg-primary-soft',
    path: '/warehouse',
  },
  {
    label: '今日入库',
    key: 'inboundToday' as const,
    icon: PackagePlus,
    color: 'text-[var(--success)]',
    bg: 'bg-[color-mix(in_oklch,var(--success)_12%,transparent)]',
    path: '/inbound',
  },
  {
    label: '待出库',
    key: 'outboundPending' as const,
    icon: PackageMinus,
    color: 'text-[var(--warning)]',
    bg: 'bg-[color-mix(in_oklch,var(--warning)_12%,transparent)]',
    path: '/outbound',
  },
  {
    label: '低库存预警',
    key: 'lowStockCount' as const,
    icon: AlertTriangle,
    color: 'text-[var(--danger)]',
    bg: 'bg-[color-mix(in_oklch,var(--danger)_12%,transparent)]',
    path: '/inventory',
  },
]
</script>

<template>
  <div>
    <PageHeader
      :title="`${greeting}，${userStore.userName || '用户'}`"
      sub="这是您的 WMS 仓储管理看板"
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
            @click="router.push('/inbound')"
          >
            <PackagePlus :size="14" class="text-muted-foreground" />
            新建入库单
          </button>
          <button
            class="flex items-center gap-2 px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[13px] text-foreground hover:border-primary/40 hover:bg-[var(--subtle)] transition-colors cursor-pointer"
            @click="router.push('/outbound')"
          >
            <PackageMinus :size="14" class="text-muted-foreground" />
            新建出库单
          </button>
          <button
            class="flex items-center gap-2 px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[13px] text-foreground hover:border-primary/40 hover:bg-[var(--subtle)] transition-colors cursor-pointer"
            @click="router.push('/inventory')"
          >
            <Warehouse :size="14" class="text-muted-foreground" />
            库存查询
          </button>
          <button
            class="flex items-center gap-2 px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[13px] text-foreground hover:border-primary/40 hover:bg-[var(--subtle)] transition-colors cursor-pointer"
            @click="router.push('/stocktake')"
          >
            <AlertTriangle :size="14" class="text-muted-foreground" />
            发起盘点
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
