<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Gavel, Package, Truck, Wallet, FileBadge, ChevronRight, Factory } from '@lucide/vue'
import { portalApi } from '@/api/portal'
import type { PortalSummary } from '@/api/types'
import { useUserStore } from '@/store/user'

const router = useRouter()
const userStore = useUserStore()

const summary = ref<PortalSummary>({ pendingQuotes: 0, activeOrders: 0, readyToShip: 0, outstandingAmount: 0 })

onMounted(async () => {
  if (!userStore.userName) userStore.loadFromOidc()
  summary.value = await portalApi.getSummary()
})

const quickEntries = [
  { path: '/statements', label: '对账中心', icon: Wallet, desc: '应付与结算' },
  { path: '/qualifications', label: '资质上传', icon: FileBadge, desc: '执照与认证' },
]

const fmtMoney = (n: number) => n.toLocaleString('zh-CN')
</script>

<template>
  <div class="pb-4">
    <!-- Header -->
    <header class="px-5 pt-8 pb-6 bg-primary-soft">
      <div class="flex items-center gap-3">
        <div class="w-11 h-11 rounded-2xl bg-primary flex items-center justify-center flex-none">
          <Factory :size="22" class="text-primary-foreground" />
        </div>
        <div class="min-w-0">
          <p class="font-semibold text-[15px] truncate">
            你好，{{ userStore.userName || '合作伙伴' }}
          </p>
          <p class="text-xs text-muted-foreground mt-0.5">欢迎回到 Octopus 供应商门户</p>
        </div>
      </div>

      <!-- Summary cards -->
      <div class="grid grid-cols-2 gap-2.5 mt-5">
        <button
          class="bg-card rounded-xl p-3.5 text-left border-0 cursor-pointer shadow-sm active:opacity-80"
          @click="router.push('/quotes')"
        >
          <p class="text-xs text-muted-foreground">待报价询价</p>
          <p class="text-xl font-semibold mt-1 text-primary">{{ summary.pendingQuotes }}</p>
        </button>
        <button
          class="bg-card rounded-xl p-3.5 text-left border-0 cursor-pointer shadow-sm active:opacity-80"
          @click="router.push('/orders')"
        >
          <p class="text-xs text-muted-foreground">进行中订单</p>
          <p class="text-xl font-semibold mt-1 text-primary">{{ summary.activeOrders }}</p>
        </button>
        <button
          class="bg-card rounded-xl p-3.5 text-left border-0 cursor-pointer shadow-sm active:opacity-80"
          @click="router.push('/orders')"
        >
          <p class="text-xs text-muted-foreground">待发货</p>
          <p class="text-xl font-semibold mt-1 text-warning">{{ summary.readyToShip }}</p>
        </button>
        <button
          class="bg-card rounded-xl p-3.5 text-left border-0 cursor-pointer shadow-sm active:opacity-80"
          @click="router.push('/statements')"
        >
          <p class="text-xs text-muted-foreground">应付金额（元）</p>
          <p class="text-xl font-semibold mt-1 text-danger">{{ fmtMoney(summary.outstandingAmount) }}</p>
        </button>
      </div>
    </header>

    <!-- Quick entries -->
    <section class="px-5 mt-5">
      <h2 class="text-sm font-semibold mb-3">常用服务</h2>
      <div class="grid grid-cols-2 gap-2.5">
        <button
          v-for="entry in quickEntries"
          :key="entry.path"
          class="bg-card rounded-xl p-3.5 flex items-center gap-3 text-left border-0 cursor-pointer shadow-sm active:opacity-80"
          @click="router.push(entry.path)"
        >
          <div class="w-9 h-9 rounded-lg bg-primary-soft flex items-center justify-center flex-none">
            <component :is="entry.icon" :size="18" class="text-primary-soft-fg" />
          </div>
          <div class="min-w-0">
            <p class="text-sm font-medium">{{ entry.label }}</p>
            <p class="text-[11px] text-muted-foreground truncate">{{ entry.desc }}</p>
          </div>
        </button>
      </div>
    </section>

    <!-- Primary CTAs -->
    <section class="px-5 mt-5">
      <button
        class="w-full bg-card rounded-xl p-4 flex items-center gap-3 border-0 cursor-pointer shadow-sm active:opacity-80"
        @click="router.push('/quotes')"
      >
        <div class="w-9 h-9 rounded-lg bg-primary-soft flex items-center justify-center flex-none">
          <Gavel :size="18" class="text-primary-soft-fg" />
        </div>
        <div class="flex-1 text-left">
          <p class="text-sm font-medium">询价响应</p>
          <p class="text-[11px] text-muted-foreground">{{ summary.pendingQuotes }} 条询价等待您报价</p>
        </div>
        <ChevronRight :size="16" class="text-muted-foreground flex-none" />
      </button>
      <button
        class="mt-2.5 w-full bg-card rounded-xl p-4 flex items-center gap-3 border-0 cursor-pointer shadow-sm active:opacity-80"
        @click="router.push('/orders')"
      >
        <div class="w-9 h-9 rounded-lg bg-primary-soft flex items-center justify-center flex-none">
          <Truck :size="18" class="text-primary-soft-fg" />
        </div>
        <div class="flex-1 text-left">
          <p class="text-sm font-medium">发货登记</p>
          <p class="text-[11px] text-muted-foreground">备货完成后登记物流单号</p>
        </div>
        <ChevronRight :size="16" class="text-muted-foreground flex-none" />
      </button>
      <button
        class="mt-2.5 w-full bg-card rounded-xl p-4 flex items-center gap-3 border-0 cursor-pointer shadow-sm active:opacity-80"
        @click="router.push('/orders')"
      >
        <div class="w-9 h-9 rounded-lg bg-primary-soft flex items-center justify-center flex-none">
          <Package :size="18" class="text-primary-soft-fg" />
        </div>
        <div class="flex-1 text-left">
          <p class="text-sm font-medium">我的订单</p>
          <p class="text-[11px] text-muted-foreground">采购订单与备货状态</p>
        </div>
        <ChevronRight :size="16" class="text-muted-foreground flex-none" />
      </button>
    </section>

    <p class="text-center text-[11px] text-muted-foreground mt-6">Mock 数据 · 接入真实 API 后自动切换</p>
  </div>
</template>
