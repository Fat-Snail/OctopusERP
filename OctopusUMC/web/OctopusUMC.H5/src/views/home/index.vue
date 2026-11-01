<script setup lang="ts">
import { computed } from 'vue'
import {
  ClipboardList, Clock, Bell, Users,
  Building2, MessageSquare, Receipt, FileText,
  ShoppingCart, Wrench, Truck, Package,
  ChevronRight
} from '@lucide/vue'
import { useUserStore } from '@/store/user'

const store = useUserStore()

const greeting = computed(() => {
  const h = new Date().getHours()
  return h < 12 ? '早上好' : h < 18 ? '下午好' : '晚上好'
})

const OA  = 'http://localhost:5174'
const WMS = 'http://localhost:5177'
const PLM = 'http://localhost:5175'
const CRM = 'http://localhost:5176'
const MES = 'http://localhost:5178'

const modules = [
  {
    system: 'OA 办公协同',
    color: '#3b82f6',
    bg: '#eff6ff',
    items: [
      { label: '我的审批', icon: ClipboardList, url: `${OA}/h5/approval` },
      { label: '考勤打卡', icon: Clock,         url: `${OA}/h5/attendance` },
      { label: '公告中心', icon: Bell,          url: `${OA}/h5/notice` },
      { label: '通讯录',   icon: Users,         url: `${OA}/h5/contact` },
    ],
  },
  {
    system: 'WMS 仓储管理',
    color: '#f59e0b',
    bg: '#fffbeb',
    items: [
      { label: '库存查询', icon: Package,    url: `${WMS}/inventory` },
      { label: '入库管理', icon: Truck,      url: `${WMS}/inbound` },
      { label: '出库管理', icon: ShoppingCart,url: `${WMS}/outbound` },
    ],
  },
  {
    system: 'MES 生产制造',
    color: '#8b5cf6',
    bg: '#f5f3ff',
    items: [
      { label: '工单管理', icon: Wrench,      url: `${MES}/workorder` },
      { label: '采购订单', icon: FileText,    url: `${MES}/purchase` },
    ],
  },
  {
    system: 'CRM 客户关系',
    color: '#ef4444',
    bg: '#fef2f2',
    items: [
      { label: '客户管理', icon: Building2,    url: `${CRM}/customer` },
      { label: '合同管理', icon: Receipt,      url: `${CRM}/contract` },
      { label: '询盘管理', icon: MessageSquare,url: `${CRM}/inquiry` },
    ],
  },
]

// PLM is imported but not used in modules array — kept for future use
void PLM
</script>

<template>
  <div class="min-h-full bg-subtle">
    <!-- Greeting header -->
    <div class="bg-primary px-5 pt-10 pb-6">
      <p class="text-white/70 text-[13px] mb-1">{{ greeting }} 👋</p>
      <h2 class="text-white text-[20px] font-semibold">{{ store.userName || '同学' }}</h2>
    </div>

    <!-- Module cards -->
    <div class="px-4 py-4 space-y-3">
      <div
        v-for="mod in modules"
        :key="mod.system"
        class="bg-background rounded-2xl border border-border overflow-hidden"
      >
        <div class="flex items-center justify-between px-4 py-3 border-b border-border">
          <span class="text-[14px] font-semibold text-foreground">{{ mod.system }}</span>
          <ChevronRight :size="16" class="text-muted-foreground" />
        </div>
        <div class="grid grid-cols-4 p-2">
          <a
            v-for="item in mod.items"
            :key="item.label"
            :href="item.url"
            class="flex flex-col items-center gap-1.5 p-3 rounded-xl active:bg-muted"
          >
            <div
              class="w-11 h-11 rounded-xl flex items-center justify-center"
              :style="`background:${mod.bg}`"
            >
              <component :is="item.icon" :size="20" :style="`color:${mod.color}`" />
            </div>
            <span class="text-[11px] text-muted-foreground text-center leading-tight">{{ item.label }}</span>
          </a>
        </div>
      </div>
    </div>
  </div>
</template>
