<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft } from '@lucide/vue'
import { portalApi } from '@/api/mock'
import type { StatementItem, StatementSummary } from '@/api/types'

const router = useRouter()
const summary = ref<StatementSummary>({ payable: 0, settled: 0, outstanding: 0 })
const items = ref<StatementItem[]>([])
const loading = ref(true)

onMounted(async () => {
  summary.value = await portalApi.getStatementSummary()
  items.value = await portalApi.getStatementItems()
  loading.value = false
})

const fmtMoney = (n: number) => n.toLocaleString('zh-CN')
</script>

<template>
  <div class="min-h-dvh bg-subtle">
    <header class="sticky top-0 z-10 bg-background px-5 pt-6 pb-3 border-b border-border flex items-center gap-3">
      <button class="border-0 bg-transparent cursor-pointer p-1 -ml-1" @click="router.back()">
        <ArrowLeft :size="20" />
      </button>
      <h1 class="text-lg font-semibold">对账中心</h1>
    </header>

    <div v-if="loading" class="px-5 py-10 text-center text-muted-foreground text-sm">加载中…</div>

    <template v-else>
      <!-- Summary -->
      <div class="px-5 pt-4">
        <div class="grid grid-cols-3 gap-2.5">
          <div class="bg-card rounded-xl p-3.5 text-center shadow-sm">
            <p class="text-[11px] text-muted-foreground">应付合计</p>
            <p class="text-sm font-semibold mt-1 text-primary">¥{{ fmtMoney(summary.payable) }}</p>
          </div>
          <div class="bg-card rounded-xl p-3.5 text-center shadow-sm">
            <p class="text-[11px] text-muted-foreground">已结算</p>
            <p class="text-sm font-semibold mt-1 text-success">¥{{ fmtMoney(summary.settled) }}</p>
          </div>
          <div class="bg-card rounded-xl p-3.5 text-center shadow-sm">
            <p class="text-[11px] text-muted-foreground">待结算</p>
            <p class="text-sm font-semibold mt-1 text-danger">¥{{ fmtMoney(summary.outstanding) }}</p>
          </div>
        </div>
      </div>

      <!-- Items -->
      <div class="px-5 mt-4 space-y-2.5">
        <h2 class="text-sm font-semibold">结算明细</h2>
        <div
          v-for="it in items"
          :key="it.id"
          class="bg-card rounded-xl p-3.5 shadow-sm"
        >
          <div class="flex items-center justify-between">
            <div class="min-w-0">
              <p class="text-sm font-medium truncate">{{ it.description }}</p>
              <p class="text-[11px] text-muted-foreground mt-0.5">{{ it.date }} · {{ it.code }}</p>
            </div>
            <div class="text-right flex-none">
              <p class="text-sm font-semibold text-primary">¥{{ fmtMoney(it.amount) }}</p>
              <p :class="['text-[10px] mt-0.5', it.status === 'pending' ? 'text-warning' : 'text-muted-foreground']">
                {{ it.status === 'pending' ? '待结算' : '已结清' }}
              </p>
            </div>
          </div>
        </div>
        <p class="text-[11px] text-muted-foreground text-center pt-2">Mock 数据 · 接入真实 API 后展示贵司的真实账务</p>
      </div>
    </template>
  </div>
</template>
