<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft, CheckCircle2 } from '@lucide/vue'
import { portalApi } from '@/api/mock'
import type { Inquiry } from '@/api/types'

const router = useRouter()

const productName = ref('')
const quantity = ref<number | null>(null)
const unit = ref('米')
const remark = ref('')
const submitting = ref(false)
const created = ref<Inquiry | null>(null)
const errorMsg = ref('')

const units = ['米', '公斤', '对', '件']

async function onSubmit() {
  errorMsg.value = ''
  if (!productName.value.trim()) { errorMsg.value = '请填写产品名称'; return }
  if (!quantity.value || quantity.value <= 0) { errorMsg.value = '请填写有效的需求数量'; return }

  submitting.value = true
  try {
    created.value = await portalApi.createInquiry({
      productName: productName.value.trim(),
      quantity: quantity.value,
      unit: unit.value,
      remark: remark.value.trim() || undefined,
    })
  } finally {
    submitting.value = false
  }
}

function backToList() {
  router.replace('/inquiries')
}
</script>

<template>
  <div class="min-h-dvh bg-subtle">
    <header class="sticky top-0 z-10 bg-background px-5 pt-6 pb-3 border-b border-border flex items-center gap-3">
      <button class="border-0 bg-transparent cursor-pointer p-1 -ml-1" @click="router.back()">
        <ArrowLeft :size="20" />
      </button>
      <h1 class="text-lg font-semibold">提交新询盘</h1>
    </header>

    <!-- 成功态 -->
    <div v-if="created" class="px-5 pt-10 flex flex-col items-center text-center">
      <CheckCircle2 :size="56" class="text-success" />
      <h2 class="text-base font-semibold mt-4">询盘已提交</h2>
      <p class="text-sm text-muted-foreground mt-1">编号 {{ created.code }}</p>
      <div class="w-full bg-card rounded-xl p-4 mt-6 shadow-sm text-left space-y-2">
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">产品</span>
          <span>{{ created.productName }}</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">数量</span>
          <span>{{ created.quantity.toLocaleString('zh-CN') }} {{ created.unit }}</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">状态</span>
          <span class="text-info">已提交，等待销售接单</span>
        </div>
      </div>
      <button
        class="mt-8 w-full h-12 rounded-xl bg-primary text-primary-foreground font-medium cursor-pointer border-0 active:opacity-80"
        @click="backToList"
      >
        返回询盘列表
      </button>
      <p class="text-xs text-muted-foreground mt-3">Mock 环境 · 报价进度将在列表页展示</p>
    </div>

    <!-- 表单态 -->
    <div v-else class="px-5 pt-4">
      <div class="bg-card rounded-xl p-4 shadow-sm space-y-4">
        <div>
          <label class="text-xs text-muted-foreground" for="product">产品名称 *</label>
          <input
            id="product"
            v-model="productName"
            type="text"
            placeholder="如：全棉卫衣面料（280g）"
            class="mt-1.5 w-full h-10 rounded-lg border border-border bg-background px-3 text-sm outline-none focus:border-primary"
          />
        </div>
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="text-xs text-muted-foreground" for="qty">需求数量 *</label>
            <input
              id="qty"
              v-model.number="quantity"
              type="number"
              min="1"
              placeholder="5000"
              class="mt-1.5 w-full h-10 rounded-lg border border-border bg-background px-3 text-sm outline-none focus:border-primary"
            />
          </div>
          <div>
            <label class="text-xs text-muted-foreground" for="unit">单位</label>
            <select
              id="unit"
              v-model="unit"
              class="mt-1.5 w-full h-10 rounded-lg border border-border bg-background px-2 text-sm outline-none focus:border-primary"
            >
              <option v-for="u in units" :key="u" :value="u">{{ u }}</option>
            </select>
          </div>
        </div>
        <div>
          <label class="text-xs text-muted-foreground" for="remark">补充说明</label>
          <textarea
            id="remark"
            v-model="remark"
            rows="3"
            placeholder="颜色、克重、用途等（选填）"
            class="mt-1.5 w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none focus:border-primary resize-none"
          />
        </div>
      </div>

      <p v-if="errorMsg" class="text-danger text-xs mt-3">{{ errorMsg }}</p>

      <button
        :disabled="submitting"
        class="mt-5 w-full h-12 rounded-xl bg-primary text-primary-foreground font-medium cursor-pointer border-0 active:opacity-80 disabled:opacity-60"
        @click="onSubmit"
      >
        {{ submitting ? '提交中…' : '提交询盘' }}
      </button>
      <p class="text-xs text-muted-foreground mt-3 text-center">提交后销售将在 2 小时内接单，报价进度可在「我的询盘」查看</p>
    </div>
  </div>
</template>
