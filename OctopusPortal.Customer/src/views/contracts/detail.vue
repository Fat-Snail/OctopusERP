<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft, FileDown } from '@lucide/vue'
import { portalApi } from '@/api/mock'
import type { Contract } from '@/api/types'
import { useUserStore } from '@/store/user'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

const item = ref<Contract | null>(null)
const downloading = ref(false)

onMounted(async () => {
  const list = await portalApi.getContracts()
  item.value = list.find((c) => c.id === String(route.params.id)) ?? null
})

const fmtMoney = (n: number) => n.toLocaleString('zh-CN')

async function downloadPdf() {
  if (!item.value) return
  downloading.value = true
  try {
    // Mock：前端生成合同摘要文本作为占位 PDF 载荷；接入真实 API 后改为下载后端生成的 PDF 文件
    const content = [
      `销售合同 ${item.value.code}`,
      `产品：${item.value.productName}`,
      `金额：¥${fmtMoney(item.value.amount)}`,
      `交期：${item.value.deliveryDate}`,
      `签订日期：${item.value.signedAt}`,
      `状态：${item.value.status === 'executing' ? '执行中' : '已完成'}`,
      `客户：${userStore.userName || '贵宾客户'}（Mock 数据）`,
    ].join('\n')
    const blob = new Blob([content], { type: 'application/pdf' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${item.value.code}.pdf`
    a.click()
    URL.revokeObjectURL(url)
  } finally {
    downloading.value = false
  }
}
</script>

<template>
  <div class="min-h-dvh bg-subtle">
    <header class="sticky top-0 z-10 bg-background px-5 pt-6 pb-3 border-b border-border flex items-center gap-3">
      <button class="border-0 bg-transparent cursor-pointer p-1 -ml-1" @click="router.back()">
        <ArrowLeft :size="20" />
      </button>
      <h1 class="text-lg font-semibold truncate">销售合同 {{ item?.code ?? '' }}</h1>
    </header>

    <div v-if="item" class="px-5 pt-4 pb-8">
      <div class="bg-card rounded-xl p-4 shadow-sm space-y-3">
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">合同金额</span>
          <span class="font-semibold text-primary text-base">¥{{ fmtMoney(item.amount) }}</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">产品</span>
          <span class="text-right">{{ item.productName }}</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">承诺交期</span>
          <span>{{ item.deliveryDate }}</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">签订日期</span>
          <span>{{ item.signedAt }}</span>
        </div>
        <div class="flex justify-between text-sm">
          <span class="text-muted-foreground">状态</span>
          <span :class="item.status === 'executing' ? 'text-warning' : 'text-success'">
            {{ item.status === 'executing' ? '执行中' : '已完成' }}
          </span>
        </div>
      </div>

      <button
        :disabled="downloading"
        class="mt-5 w-full h-12 rounded-xl bg-primary text-primary-foreground font-medium cursor-pointer border-0 active:opacity-80 disabled:opacity-60 flex items-center justify-center gap-2"
        @click="downloadPdf"
      >
        <FileDown :size="18" />
        {{ downloading ? '生成中…' : '下载合同 PDF' }}
      </button>
      <p class="text-xs text-muted-foreground mt-3 text-center">Mock 环境 · 下载内容为合同摘要占位文件</p>
    </div>

    <div v-else class="px-5 py-10 text-center text-muted-foreground text-sm">合同不存在</div>
  </div>
</template>
