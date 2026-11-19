<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  MessageSquareText,
  FileText,
  Truck,
  Wallet,
  Headset,
  FolderDown,
  ChevronRight,
  Container,
} from '@lucide/vue'
import { portalApi } from '@/api/portal'
import type { PortalSummary } from '@/api/types'
import { useUserStore } from '@/store/user'

const router = useRouter()
const userStore = useUserStore()

const summary = ref<PortalSummary>({ openInquiries: 0, activeContracts: 0, shippingOrders: 0, unpaidAmount: 0 })

onMounted(async () => {
  if (!userStore.userName) userStore.loadFromOidc()
  summary.value = await portalApi.getSummary()
})

const quickEntries = [
  { path: '/logistics', label: '物流追踪', icon: Truck, desc: '发货单与轨迹' },
  { path: '/statements', label: '对账中心', icon: Wallet, desc: '应收与结算' },
  { path: '/support', label: '在线客服', icon: Headset, desc: '专属客户经理' },
  { path: '/documents', label: '文档下载', icon: FolderDown, desc: '手册与文档' },
]

const fmtMoney = (n: number) => n.toLocaleString('zh-CN')
</script>

<template>
  <div class="pb-4">
    <!-- Header -->
    <header class="px-5 pt-8 pb-6 bg-primary-soft">
      <div class="flex items-center gap-3">
        <div class="w-11 h-11 rounded-2xl bg-primary flex items-center justify-center flex-none">
          <Container :size="22" class="text-primary-foreground" />
        </div>
        <div class="min-w-0">
          <p class="font-semibold text-[15px] truncate">
            你好，{{ userStore.userName || '贵宾客户' }}
          </p>
          <p class="text-xs text-muted-foreground mt-0.5">欢迎回到 Octopus 客户门户</p>
        </div>
      </div>

      <!-- Summary cards -->
      <div class="grid grid-cols-2 gap-2.5 mt-5">
        <button
          class="bg-card rounded-xl p-3.5 text-left border-0 cursor-pointer shadow-sm active:opacity-80"
          @click="router.push('/inquiries')"
        >
          <p class="text-xs text-muted-foreground">进行中询盘</p>
          <p class="text-xl font-semibold mt-1 text-primary">{{ summary.openInquiries }}</p>
        </button>
        <button
          class="bg-card rounded-xl p-3.5 text-left border-0 cursor-pointer shadow-sm active:opacity-80"
          @click="router.push('/contracts')"
        >
          <p class="text-xs text-muted-foreground">执行中合同</p>
          <p class="text-xl font-semibold mt-1 text-primary">{{ summary.activeContracts }}</p>
        </button>
        <button
          class="bg-card rounded-xl p-3.5 text-left border-0 cursor-pointer shadow-sm active:opacity-80"
          @click="router.push('/logistics')"
        >
          <p class="text-xs text-muted-foreground">在途发货</p>
          <p class="text-xl font-semibold mt-1 text-info">{{ summary.shippingOrders }}</p>
        </button>
        <button
          class="bg-card rounded-xl p-3.5 text-left border-0 cursor-pointer shadow-sm active:opacity-80"
          @click="router.push('/statements')"
        >
          <p class="text-xs text-muted-foreground">待付金额（元）</p>
          <p class="text-xl font-semibold mt-1 text-danger">{{ fmtMoney(summary.unpaidAmount) }}</p>
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

    <!-- Inquiry CTA -->
    <section class="px-5 mt-5">
      <button
        class="w-full bg-card rounded-xl p-4 flex items-center gap-3 border-0 cursor-pointer shadow-sm active:opacity-80"
        @click="router.push('/inquiries/new')"
      >
        <div class="w-9 h-9 rounded-lg bg-primary-soft flex items-center justify-center flex-none">
          <MessageSquareText :size="18" class="text-primary-soft-fg" />
        </div>
        <div class="flex-1 text-left">
          <p class="text-sm font-medium">提交新询盘</p>
          <p class="text-[11px] text-muted-foreground">告诉我们您需要的面料与数量</p>
        </div>
        <ChevronRight :size="16" class="text-muted-foreground flex-none" />
      </button>
      <button
        class="mt-2.5 w-full bg-card rounded-xl p-4 flex items-center gap-3 border-0 cursor-pointer shadow-sm active:opacity-80"
        @click="router.push('/contracts')"
      >
        <div class="w-9 h-9 rounded-lg bg-primary-soft flex items-center justify-center flex-none">
          <FileText :size="18" class="text-primary-soft-fg" />
        </div>
        <div class="flex-1 text-left">
          <p class="text-sm font-medium">我的合同</p>
          <p class="text-[11px] text-muted-foreground">查看合同状态与交期</p>
        </div>
        <ChevronRight :size="16" class="text-muted-foreground flex-none" />
      </button>
    </section>

    <p class="text-center text-[11px] text-muted-foreground mt-6">Mock 数据 · 接入真实 API 后自动切换</p>
  </div>
</template>
