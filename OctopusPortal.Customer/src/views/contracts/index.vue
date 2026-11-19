<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ChevronRight } from '@lucide/vue'
import { portalApi } from '@/api/portal'
import type { Contract } from '@/api/types'

const router = useRouter()
const contracts = ref<Contract[]>([])
const loading = ref(true)

onMounted(async () => {
  contracts.value = await portalApi.getContracts()
  loading.value = false
})

const fmtMoney = (n: number) => n.toLocaleString('zh-CN')
</script>

<template>
  <div class="pb-4">
    <header class="sticky top-0 z-10 bg-background px-5 pt-6 pb-3 border-b border-border">
      <h1 class="text-lg font-semibold">我的合同</h1>
    </header>

    <div v-if="loading" class="px-5 py-10 text-center text-muted-foreground text-sm">加载中…</div>

    <div v-else class="px-5 mt-3 space-y-3">
      <button
        v-for="c in contracts"
        :key="c.id"
        class="w-full bg-card rounded-xl p-4 text-left border-0 cursor-pointer shadow-sm active:opacity-80"
        @click="router.push(`/contracts/${c.id}`)"
      >
        <div class="flex items-start justify-between gap-2">
          <div class="min-w-0">
            <p class="text-[11px] text-muted-foreground">{{ c.code }}</p>
            <p class="text-sm font-medium mt-0.5 truncate">{{ c.productName }}</p>
          </div>
          <ChevronRight :size="16" class="text-muted-foreground flex-none mt-1" />
        </div>
        <div class="flex items-center justify-between mt-2.5">
          <p class="text-base font-semibold text-primary">¥{{ fmtMoney(c.amount) }}</p>
          <span :class="[
            'text-[11px] px-2 py-0.5 rounded-full',
            c.status === 'executing' ? 'text-warning bg-warning/10' : 'text-success bg-success/10',
          ]">
            {{ c.status === 'executing' ? '执行中' : '已完成' }}
          </span>
        </div>
        <p class="text-[11px] text-muted-foreground mt-1.5">交期 {{ c.deliveryDate }} · 签订 {{ c.signedAt }}</p>
      </button>
    </div>
  </div>
</template>
