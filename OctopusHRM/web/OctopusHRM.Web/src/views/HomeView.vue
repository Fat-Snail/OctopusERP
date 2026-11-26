<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { Users, Wallet, CalendarDays, ClipboardList } from '@lucide/vue'
import { useUserStore } from '@/store/modules/user'
import { getEmployees, getPayrollRuns, type Employee, type PayrollRun } from '@/api'

const router = useRouter()
const userStore = useUserStore()
const employees = ref<Employee[]>([])
const payrolls = ref<PayrollRun[]>([])

const greeting = computed(() => {
  const h = new Date().getHours()
  return h < 12 ? '早上好' : h < 18 ? '下午好' : '晚上好'
})
const activeCount = computed(() => employees.value.filter(e => e.status !== 'resigned').length)
const latestPayroll = computed(() => payrolls.value[0])

const cards = computed(() => [
  { label: '在职员工', value: String(activeCount.value), icon: Users, path: '/employees', color: 'text-[var(--primary-soft-fg)]', bg: 'bg-primary-soft' },
  { label: '员工总数', value: String(employees.value.length), icon: ClipboardList, path: '/employees', color: 'text-[var(--info)]', bg: 'bg-[color-mix(in_oklch,var(--info)_12%,transparent)]' },
  { label: '最新算薪期间', value: latestPayroll.value?.periodCode ?? '—', icon: Wallet, path: '/payroll', color: 'text-[var(--success)]', bg: 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)]' },
  { label: '最近实发 (¥)', value: Number(latestPayroll.value?.totalNetPay ?? 0).toLocaleString('zh-CN', { maximumFractionDigits: 0 }), icon: CalendarDays, path: '/payroll', color: 'text-[var(--warning)]', bg: 'bg-[color-mix(in_oklch,var(--warning)_14%,transparent)]' },
])

onMounted(async () => {
  const [e, p] = await Promise.allSettled([getEmployees(), getPayrollRuns()])
  if (e.status === 'fulfilled') employees.value = e.value
  if (p.status === 'fulfilled') payrolls.value = p.value
})
</script>

<template>
  <div>
    <div class="px-5 pt-4 pb-3 bg-card border-b border-border">
      <h1 class="text-[18px] font-semibold text-foreground">{{ greeting }}，{{ userStore.userName || '用户' }}</h1>
      <p class="text-[12px] text-muted-foreground mt-0.5">人力概览 · 组织架构与薪酬绩效</p>
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
          <button v-for="q in [{ label: '员工档案', path: '/employees' }, { label: '薪资核算', path: '/payroll' }, { label: '假期与绩效', path: '/leave-kpi' }]" :key="q.path" class="px-4 py-2 bg-card border border-border rounded-[var(--radius)] text-[12px] hover:border-primary/40 cursor-pointer" @click="router.push(q.path)">{{ q.label }}</button>
        </div>
      </div>
    </div>
  </div>
</template>
