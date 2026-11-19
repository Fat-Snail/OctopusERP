<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Truck } from '@lucide/vue'
import { portalApi } from '@/api/portal'
import type { OrderStatus, PurchaseOrder } from '@/api/types'

const router = useRouter()
const orders = ref<PurchaseOrder[]>([])
const loading = ref(true)
const filter = ref<'all' | 'open' | 'done'>('all')

onMounted(async () => {
  orders.value = await portalApi.getOrders()
  loading.value = false
})

const statusMeta: Record<OrderStatus, { label: string; cls: string }> = {
  preparing: { label: '备货中', cls: 'text-warning bg-warning/10' },
  producing: { label: '生产中', cls: 'text-warning bg-warning/10' },
  ready: { label: '待发货', cls: 'text-primary-soft-fg bg-primary-soft' },
  shipped: { label: '已发货', cls: 'text-info bg-info/10' },
  completed: { label: '已完成', cls: 'text-success bg-success/10' },
}

const filtered = computed(() =>
  filter.value === 'all'
    ? orders.value
    : orders.value.filter((o) =>
        filter.value === 'done' ? o.status === 'completed' : o.status !== 'completed'
      )
)

const fmtMoney = (n: number) => n.toLocaleString('zh-CN')
</script>

<template>
  <div class="pb-4">
    <header class="sticky top-0 z-10 bg-background/90 backdrop-blur px-5 pt-6 pb-3 border-b border-border">
      <h1 class="text-lg font-semibold">我的订单</h1>
      <div class="flex gap-2 mt-3">
        <button
          v-for="f in [
            { key: 'all', label: '全部' },
            { key: 'open', label: '进行中' },
            { key: 'done', label: '已完成' },
          ]"
          :key="f.key"
          :class="[
            'h-7 px-3 rounded-full text-xs border-0 cursor-pointer transition-colors',
            filter === f.key ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground',
          ]"
          @click="filter = f.key as 'all' | 'open' | 'done'"
        >
          {{ f.label }}
        </button>
      </div>
    </header>

    <div v-if="loading" class="px-5 py-10 text-center text-muted-foreground text-sm">加载中…</div>

    <div v-else class="px-5 mt-3 space-y-3">
      <div
        v-for="o in filtered"
        :key="o.id"
        class="bg-card rounded-xl p-4 shadow-sm"
      >
        <div class="flex items-start justify-between gap-2">
          <div class="min-w-0">
            <p class="text-[11px] text-muted-foreground">{{ o.code }} · 下单 {{ o.createdAt }}</p>
            <p class="text-sm font-medium mt-0.5 truncate">{{ o.productName }}</p>
          </div>
          <span :class="['text-[11px] px-2 py-0.5 rounded-full flex-none', statusMeta[o.status].cls]">
            {{ statusMeta[o.status].label }}
          </span>
        </div>

        <div class="flex items-center justify-between mt-2.5">
          <p class="text-base font-semibold text-primary">¥{{ fmtMoney(o.amount) }}</p>
          <span class="text-[11px] text-muted-foreground">计划交期 {{ o.planDeliveryDate }}</span>
        </div>

        <button
          v-if="o.status === 'ready'"
          class="mt-3 w-full h-9 rounded-lg bg-primary text-primary-foreground text-xs font-medium border-0 cursor-pointer active:opacity-80 flex items-center justify-center gap-1.5"
          @click="router.push(`/orders/ship/${o.id}`)"
        >
          <Truck :size="14" /> 发货登记
        </button>
      </div>
    </div>
  </div>
</template>
