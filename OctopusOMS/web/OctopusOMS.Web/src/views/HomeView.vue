<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ShoppingCart, Store, Link2, Truck } from '@lucide/vue'
import { useUserStore } from '@/store/modules/user'
import { getChannels, getShops, getAdapters, getOrders, getAfterSales, type Channel, type Shop, type AdapterInfo } from '@/api'

const router = useRouter()
const userStore = useUserStore()
const channels = ref<Channel[]>([])
const shops = ref<Shop[]>([])
const adapters = ref<AdapterInfo[]>([])
const paidCount = ref(0)
const shippedCount = ref(0)
const pendingAfterSale = ref(0)

const greeting = computed(() => {
  const h = new Date().getHours()
  return h < 12 ? '早上好' : h < 18 ? '下午好' : '晚上好'
})
const liveAdapters = computed(() => adapters.value.filter(a => a.mode === 'live').length)

const cards = computed(() => [
  { label: '渠道数', value: String(channels.value.length), icon: Store, path: '/channels', color: 'text-[var(--primary-soft-fg)]', bg: 'bg-primary-soft' },
  { label: '店铺数', value: String(shops.value.length), icon: Link2, path: '/channels', color: 'text-[var(--info)]', bg: 'bg-[color-mix(in_oklch,var(--info)_12%,transparent)]' },
  { label: '已支付订单', value: String(paidCount.value), icon: ShoppingCart, path: '/orders?status=paid', color: 'text-[var(--warning)]', bg: 'bg-[color-mix(in_oklch,var(--warning)_14%,transparent)]' },
  { label: '待处理售后', value: String(pendingAfterSale.value), icon: Truck, path: '/after-sales', color: 'text-[var(--success)]', bg: 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)]' },
])

onMounted(async () => {
  const [c, s, a, o1, o2, as] = await Promise.allSettled([
    getChannels(), getShops(), getAdapters(),
    getOrders({ status: 'paid', pageNum: 1, pageSize: 1 }),
    getOrders({ status: 'shipped', pageNum: 1, pageSize: 1 }),
    getAfterSales('pending'),
  ])
  if (c.status === 'fulfilled') channels.value = c.value
  if (s.status === 'fulfilled') shops.value = s.value
  if (a.status === 'fulfilled') adapters.value = a.value
  if (o1.status === 'fulfilled') paidCount.value = o1.value.total
  if (o2.status === 'fulfilled') shippedCount.value = o2.value.total
  if (as.status === 'fulfilled') pendingAfterSale.value = as.value.length
})
</script>

<template>
  <div>
    <div class="px-5 pt-4 pb-3 bg-card border-b border-border">
      <h1 class="text-[18px] font-semibold text-foreground">{{ greeting }}，{{ userStore.userName || '用户' }}</h1>
      <p class="text-[12px] text-muted-foreground mt-0.5">订单中台概览 · {{ liveAdapters }} 个适配器已接入实时模式 · 已发货 {{ shippedCount }} 单</p>
    </div>
    <div class="p-5 space-y-5">
      <div class="grid grid-cols-4 gap-4">
        <div v-for="c in cards" :key="c.label" class="bg-card border border-border rounded-[var(--radius-lg)] p-4 cursor-pointer hover:border-primary/40 transition-colors" @click="router.push(c.path)">
          <div class="flex items-start justify-between mb-3">
            <span class="text-[12px] text-muted-foreground">{{ c.label }}</span>
            <div :class="[c.bg, 'w-8 h-8 rounded-lg flex items-center justify-center']"><component :is="c.icon" :size="16" :class="c.color" /></div>
          </div>
          <div class="text-[20px] font-semibold font-mono-tnum text-foreground truncate">{{ c.value }}</div>
        </div>
      </div>
      <div>
        <h2 class="text-[14px] font-semibold text-foreground mb-3">快捷入口</h2>
        <div class="flex flex-wrap gap-3">
          <button v-for="q in [{ label: '渠道与店铺', path: '/channels' }, { label: '订单管理', path: '/orders' }, { label: 'SKU 映射', path: '/mappings' }, { label: '售后管理', path: '/after-sales' }]" :key="q.path" class="px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[12px] hover:border-primary/40 cursor-pointer" @click="router.push(q.path)">{{ q.label }}</button>
        </div>
      </div>
    </div>
  </div>
</template>
