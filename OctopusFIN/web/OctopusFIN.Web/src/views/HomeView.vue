<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { FileText, Landmark, ReceiptText, CalendarRange } from '@lucide/vue'
import { useUserStore } from '@/store/modules/user'
import { getCurrentPeriod, getAccounts, getVouchers, getArList, getApList, type Period } from '@/api'

const router = useRouter()
const userStore = useUserStore()
const period = ref<Period | null>(null)
const accountCount = ref(0)
const draftCount = ref(0)
const arAmount = ref(0)
const apAmount = ref(0)

const greeting = computed(() => {
  const h = new Date().getHours()
  return h < 12 ? '早上好' : h < 18 ? '下午好' : '晚上好'
})

const cards = computed(() => [
  { label: '当前会计期间', value: period.value?.periodCode ?? '—', icon: CalendarRange, path: '/periods', color: 'text-[var(--primary-soft-fg)]', bg: 'bg-primary-soft' },
  { label: '会计科目数', value: String(accountCount.value), icon: Landmark, path: '/accounts', color: 'text-[var(--info)]', bg: 'bg-[color-mix(in_oklch,var(--info)_12%,transparent)]' },
  { label: '待过账凭证', value: String(draftCount.value), icon: FileText, path: '/vouchers', color: 'text-[var(--warning)]', bg: 'bg-[color-mix(in_oklch,var(--warning)_14%,transparent)]' },
  { label: '应收未结 (¥)', value: arAmount.value.toLocaleString('zh-CN', { maximumFractionDigits: 0 }), icon: ReceiptText, path: '/ar-ap', color: 'text-[var(--success)]', bg: 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)]' },
])

onMounted(async () => {
  const [p, a, v, ar, ap] = await Promise.allSettled([
    getCurrentPeriod(), getAccounts(), getVouchers({ status: 'draft', pageNum: 1, pageSize: 1 }), getArList(), getApList(),
  ])
  if (p.status === 'fulfilled') period.value = p.value
  if (a.status === 'fulfilled') accountCount.value = a.value.length
  if (v.status === 'fulfilled') draftCount.value = v.value.total
  if (ar.status === 'fulfilled') arAmount.value = ar.value.reduce((s, r) => s + (r.amount - r.receivedAmount), 0)
  if (ap.status === 'fulfilled') apAmount.value = ap.value.reduce((s, r) => s + (r.amount - r.paidAmount), 0)
})
</script>

<template>
  <div>
    <div class="px-5 pt-4 pb-3 bg-card border-b border-border">
      <h1 class="text-[18px] font-semibold text-foreground">{{ greeting }}，{{ userStore.userName || '用户' }}</h1>
      <p class="text-[12px] text-muted-foreground mt-0.5">财务概览 · 应付未结 ¥ {{ apAmount.toLocaleString('zh-CN', { maximumFractionDigits: 0 }) }}</p>
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
          <button v-for="q in [{ label: '录入凭证', path: '/vouchers' }, { label: '会计科目', path: '/accounts' }, { label: '应收应付', path: '/ar-ap' }, { label: '财务报表', path: '/reports' }]" :key="q.path" class="px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[12px] hover:border-primary/40 cursor-pointer" @click="router.push(q.path)">{{ q.label }}</button>
        </div>
      </div>
    </div>
  </div>
</template>
