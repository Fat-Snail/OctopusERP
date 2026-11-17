<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft, CheckCircle2 } from '@lucide/vue'
import { portalApi } from '@/api/mock'
import type { PurchaseOrder } from '@/api/types'

const route = useRoute()
const router = useRouter()

const order = ref<PurchaseOrder | null>(null)
const carrier = ref('顺丰速运')
const trackingNo = ref('')
const submitting = ref(false)
const done = ref<{ orderCode: string; trackingNo: string; carrier: string; shippedAt: string } | null>(null)
const errorMsg = ref('')

onMounted(async () => {
  const list = await portalApi.getOrders()
  order.value = list.find((o) => o.id === String(route.params.id)) ?? null
})

const carriers = ['顺丰速运', '德邦物流', '中通快运', '圆通速递', '自行配送']

async function onSubmit() {
  errorMsg.value = ''
  if (!order.value) return
  if (!trackingNo.value.trim()) { errorMsg.value = '请填写物流单号'; return }

  submitting.value = true
  try {
    done.value = await portalApi.registerShipment(order.value.id, {
      trackingNo: trackingNo.value.trim(),
      carrier: carrier.value,
    })
  } catch (e) {
    errorMsg.value = (e as Error).message
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="min-h-dvh bg-subtle">
    <header class="sticky top-0 z-10 bg-background px-5 pt-6 pb-3 border-b border-border flex items-center gap-3">
      <button class="border-0 bg-transparent cursor-pointer p-1 -ml-1" @click="router.back()">
        <ArrowLeft :size="20" />
      </button>
      <h1 class="text-lg font-semibold">发货登记</h1>
    </header>

    <div v-if="!order" class="px-5 py-10 text-center text-muted-foreground text-sm">订单不存在</div>

    <!-- 成功态 -->
    <div v-else-if="done" class="px-5 pt-10 flex flex-col items-center text-center">
      <CheckCircle2 :size="56" class="text-success" />
      <h2 class="text-base font-semibold mt-4">发货登记成功</h2>
      <p class="text-sm text-muted-foreground mt-1">{{ done.orderCode }}</p>
      <div class="w-full bg-card rounded-xl p-4 mt-6 shadow-sm text-left space-y-2">
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">承运商</span>
          <span>{{ done.carrier }}</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">物流单号</span>
          <span class="font-medium">{{ done.trackingNo }}</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">发货时间</span>
          <span>{{ done.shippedAt }}</span>
        </div>
      </div>
      <button
        class="mt-8 w-full h-12 rounded-xl bg-primary text-primary-foreground font-medium cursor-pointer border-0 active:opacity-80"
        @click="router.replace('/orders')"
      >
        返回订单列表
      </button>
      <p class="text-xs text-muted-foreground mt-3">采购方将在发货回调后更新合同履约状态（Mock）</p>
    </div>

    <!-- 表单态 -->
    <div v-else class="px-5 pt-4">
      <div class="bg-card rounded-xl p-4 shadow-sm">
        <p class="text-[11px] text-muted-foreground">{{ order.code }}</p>
        <p class="text-sm font-medium mt-1">{{ order.productName }}</p>
        <p class="text-xs text-muted-foreground mt-1">
          {{ order.quantity.toLocaleString('zh-CN') }} {{ order.unit }} · 计划交期 {{ order.planDeliveryDate }}
        </p>
      </div>

      <div class="bg-card rounded-xl p-4 shadow-sm space-y-4 mt-3">
        <div>
          <label class="text-xs text-muted-foreground" for="carrier">承运商</label>
          <select
            id="carrier"
            v-model="carrier"
            class="mt-1.5 w-full h-10 rounded-lg border border-border bg-background px-2 text-sm outline-none focus:border-primary"
          >
            <option v-for="c in carriers" :key="c" :value="c">{{ c }}</option>
          </select>
        </div>
        <div>
          <label class="text-xs text-muted-foreground" for="tracking">物流单号 *</label>
          <input
            id="tracking"
            v-model="trackingNo"
            type="text"
            placeholder="SF1388202611007"
            class="mt-1.5 w-full h-10 rounded-lg border border-border bg-background px-3 text-sm outline-none focus:border-primary"
          />
        </div>
      </div>

      <p v-if="errorMsg" class="text-danger text-xs mt-3">{{ errorMsg }}</p>

      <button
        :disabled="submitting"
        class="mt-5 w-full h-12 rounded-xl bg-primary text-primary-foreground font-medium cursor-pointer border-0 active:opacity-80 disabled:opacity-60"
        @click="onSubmit"
      >
        {{ submitting ? '提交中…' : '确认发货' }}
      </button>
      <p class="text-xs text-muted-foreground mt-3 text-center">确认后采购方将收到发货通知，请确保单号准确</p>
    </div>
  </div>
</template>
