<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft, CheckCircle2 } from '@lucide/vue'
import { portalApi } from '@/api/portal'
import type { QuoteRequest } from '@/api/types'

const route = useRoute()
const router = useRouter()

const quote = ref<QuoteRequest | null>(null)
const unitPrice = ref<number | null>(null)
const deliveryDays = ref<number | null>(null)
const remark = ref('')
const submitting = ref(false)
const done = ref(false)
const errorMsg = ref('')

onMounted(async () => {
  const list = await portalApi.getQuoteRequests()
  quote.value = list.find((q) => q.id === String(route.params.id)) ?? null
})

async function onSubmit() {
  errorMsg.value = ''
  if (!quote.value) return
  if (!unitPrice.value || unitPrice.value <= 0) { errorMsg.value = '请填写有效的报价单价'; return }
  if (!deliveryDays.value || deliveryDays.value <= 0) { errorMsg.value = '请填写有效的交货天数'; return }

  submitting.value = true
  try {
    quote.value = await portalApi.submitQuote(quote.value.id, {
      unitPrice: unitPrice.value,
      deliveryDays: deliveryDays.value,
      remark: remark.value.trim() || undefined,
    })
    done.value = true
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
      <h1 class="text-lg font-semibold">在线报价</h1>
    </header>

    <div v-if="!quote" class="px-5 py-10 text-center text-muted-foreground text-sm">询价单不存在</div>

    <!-- 成功态 -->
    <div v-else-if="done && quote.myQuote" class="px-5 pt-10 flex flex-col items-center text-center">
      <CheckCircle2 :size="56" class="text-success" />
      <h2 class="text-base font-semibold mt-4">报价已提交</h2>
      <p class="text-sm text-muted-foreground mt-1">{{ quote.code }}</p>
      <div class="w-full bg-card rounded-xl p-4 mt-6 shadow-sm text-left space-y-2">
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">单价</span>
          <span class="font-semibold text-primary">¥{{ quote.myQuote.unitPrice }}/{{ quote.unit }}</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">总报价</span>
          <span>¥{{ (quote.myQuote.unitPrice * quote.quantity).toLocaleString('zh-CN') }}</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">交期</span>
          <span>{{ quote.myQuote.deliveryDays }} 天</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">状态</span>
          <span class="text-info">审核中</span>
        </div>
      </div>
      <button
        class="mt-8 w-full h-12 rounded-xl bg-primary text-primary-foreground font-medium cursor-pointer border-0 active:opacity-80"
        @click="router.replace('/quotes')"
      >
        返回询价列表
      </button>
    </div>

    <!-- 表单态 -->
    <div v-else class="px-5 pt-4">
      <div class="bg-card rounded-xl p-4 shadow-sm">
        <p class="text-[11px] text-muted-foreground">{{ quote.code }} · 截止 {{ quote.deadline }}</p>
        <p class="text-sm font-medium mt-1">{{ quote.productName }}</p>
        <p class="text-xs text-muted-foreground mt-1">
          采购需求 {{ quote.quantity.toLocaleString('zh-CN') }} {{ quote.unit }} · SKU {{ quote.sku }}
        </p>
      </div>

      <div class="bg-card rounded-xl p-4 shadow-sm space-y-4 mt-3">
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="text-xs text-muted-foreground" for="price">单价（元/{{ quote.unit }}）*</label>
            <input
              id="price"
              v-model.number="unitPrice"
              type="number"
              step="0.01"
              min="0"
              placeholder="2.98"
              class="mt-1.5 w-full h-10 rounded-lg border border-border bg-background px-3 text-sm outline-none focus:border-primary"
            />
          </div>
          <div>
            <label class="text-xs text-muted-foreground" for="days">交货天数 *</label>
            <input
              id="days"
              v-model.number="deliveryDays"
              type="number"
              min="1"
              placeholder="10"
              class="mt-1.5 w-full h-10 rounded-lg border border-border bg-background px-3 text-sm outline-none focus:border-primary"
            />
          </div>
        </div>
        <div>
          <label class="text-xs text-muted-foreground" for="remark">报价说明</label>
          <textarea
            id="remark"
            v-model="remark"
            rows="3"
            placeholder="含税说明、付款条件等（选填）"
            class="mt-1.5 w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none focus:border-primary resize-none"
          />
        </div>
        <div v-if="unitPrice && unitPrice > 0" class="rounded-lg bg-muted px-3 py-2.5 text-sm">
          <div class="flex justify-between">
            <span class="text-muted-foreground">报价总额预估</span>
            <span class="font-semibold text-primary">¥{{ (unitPrice * quote.quantity).toLocaleString('zh-CN') }}</span>
          </div>
        </div>
      </div>

      <p v-if="errorMsg" class="text-danger text-xs mt-3">{{ errorMsg }}</p>

      <button
        :disabled="submitting"
        class="mt-5 w-full h-12 rounded-xl bg-primary text-primary-foreground font-medium cursor-pointer border-0 active:opacity-80 disabled:opacity-60"
        @click="onSubmit"
      >
        {{ submitting ? '提交中…' : '提交报价' }}
      </button>
      <p class="text-xs text-muted-foreground mt-3 text-center">报价提交后进入审核，中标结果将在列表页展示</p>
    </div>
  </div>
</template>
