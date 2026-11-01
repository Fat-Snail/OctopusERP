<script setup lang="ts">
import { computed } from 'vue'
import {
  ClipboardList, Clock, Bell, Users,
  PackagePlus, PackageMinus, Package, ClipboardCheck as Stocktake,
  ClipboardCheck, Box, Tags, Share2,
  Building2, MessageSquare, Receipt, FileText,
  ShoppingCart, Wrench, Truck, BarChart2,
} from '@lucide/vue'
import { useUserStore } from '@/store/modules/user'

const userStore = useUserStore()

const greeting = computed(() => {
  const h = new Date().getHours()
  return h < 12 ? '早上好' : h < 18 ? '下午好' : '晚上好'
})

const displayName = computed(() =>
  userStore.userInfo?.nickName || userStore.userInfo?.userName || ''
)

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
      { label: '我的审批', icon: ClipboardList,  url: `${OA}/h5/approval`   },
      { label: '考勤打卡', icon: Clock,           url: `${OA}/h5/attendance` },
      { label: '公告中心', icon: Bell,            url: `${OA}/h5/notice`     },
      { label: '通讯录',   icon: Users,           url: `${OA}/h5/contact`    },
    ],
  },
  {
    system: 'WMS 仓储管理',
    color: '#f59e0b',
    bg: '#fffbeb',
    items: [
      { label: '入库管理', icon: PackagePlus,  url: `${WMS}/inbound`   },
      { label: '出库管理', icon: PackageMinus, url: `${WMS}/outbound`  },
      { label: '库存查询', icon: Package,      url: `${WMS}/inventory` },
      { label: '盘点管理', icon: Stocktake,    url: `${WMS}/stocktake` },
    ],
  },
  {
    system: 'PLM 商品管理',
    color: '#10b981',
    bg: '#ecfdf5',
    items: [
      { label: '待审核',   icon: ClipboardCheck, url: `${PLM}/plm/product`  },
      { label: '商品管理', icon: Box,            url: `${PLM}/plm/product`  },
      { label: '类目管理', icon: Tags,           url: `${PLM}/plm/category` },
      { label: '渠道管理', icon: Share2,         url: `${PLM}/plm/channel`  },
    ],
  },
  {
    system: 'CRM 客户管理',
    color: '#8b5cf6',
    bg: '#f5f3ff',
    items: [
      { label: '客户管理', icon: Building2,     url: `${CRM}/customer` },
      { label: '询盘管理', icon: MessageSquare, url: `${CRM}/inquiry`  },
      { label: '报价管理', icon: Receipt,       url: `${CRM}/quote`    },
      { label: '合同管理', icon: FileText,      url: `${CRM}/contract` },
    ],
  },
  {
    system: 'MES 生产执行',
    color: '#ef4444',
    bg: '#fef2f2',
    items: [
      { label: '采购订单', icon: ShoppingCart, url: `${MES}/purchase`  },
      { label: '工单管理', icon: Wrench,       url: `${MES}/workorder` },
      { label: '供应商',   icon: Truck,        url: `${MES}/supplier`  },
      { label: '数据统计', icon: BarChart2,    url: `${MES}/home`      },
    ],
  },
]

function open(url: string) {
  window.location.href = url
}
</script>

<template>
  <div class="min-h-full pb-6">
    <!-- Gradient header -->
    <div class="px-5 pt-10 pb-10" style="background:linear-gradient(135deg,oklch(0.42 0.22 255),oklch(0.52 0.19 265))">
      <p class="text-[13px] mb-0.5" style="color:rgba(255,255,255,0.75)">{{ greeting }}，</p>
      <h1 class="text-[22px] font-bold" style="color:#fff">{{ displayName }}</h1>
      <p class="text-[12px] mt-1" style="color:rgba(255,255,255,0.6)">OctopusERP 企业应用中心</p>
    </div>

    <!-- Module cards (overlap the header slightly) -->
    <div class="px-4 -mt-4 space-y-3">
      <div
        v-for="mod in modules"
        :key="mod.system"
        class="bg-background rounded-2xl overflow-hidden"
        style="box-shadow:0 1px 6px rgba(0,0,0,0.06)"
      >
        <!-- Section title -->
        <div class="flex items-center gap-2 px-4 pt-4 pb-3">
          <span class="w-[3px] h-[14px] rounded-full shrink-0" :style="`background:${mod.color}`" />
          <span class="text-[13px] font-semibold text-foreground">{{ mod.system }}</span>
        </div>

        <!-- 4-column icon grid -->
        <div class="grid grid-cols-4 pb-3 px-1">
          <button
            v-for="item in mod.items"
            :key="item.label"
            class="flex flex-col items-center gap-1.5 py-2 px-1 border-0 bg-transparent cursor-pointer active:opacity-70"
            @click="open(item.url)"
          >
            <div
              class="w-12 h-12 rounded-2xl flex items-center justify-center shrink-0"
              :style="`background:${mod.bg}`"
            >
              <component :is="item.icon" :size="22" :style="`color:${mod.color}`" />
            </div>
            <span class="text-[11px] text-center leading-tight" style="color:#374151">{{ item.label }}</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
