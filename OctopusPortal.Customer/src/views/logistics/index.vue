<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft, Truck } from '@lucide/vue'
import { portalApi } from '@/api/mock'
import type { Shipment, ShipmentStatus } from '@/api/types'

const router = useRouter()
const shipments = ref<Shipment[]>([])
const loading = ref(true)
const activeId = ref<string | null>(null)

onMounted(async () => {
  shipments.value = await portalApi.getShipments()
  loading.value = false
  if (shipments.value.length > 0) activeId.value = shipments.value[0]!.id
})

const statusMeta: Record<ShipmentStatus, { label: string; cls: string }> = {
  preparing: { label: '备货中', cls: 'text-warning bg-warning/10' },
  shipping: { label: '运输中', cls: 'text-info bg-info/10' },
  delivered: { label: '已签收', cls: 'text-success bg-success/10' },
}

function toggle(id: string) {
  activeId.value = activeId.value === id ? null : id
}
</script>

<template>
  <div class="min-h-dvh bg-subtle">
    <header class="sticky top-0 z-10 bg-background px-5 pt-6 pb-3 border-b border-border flex items-center gap-3">
      <button class="border-0 bg-transparent cursor-pointer p-1 -ml-1" @click="router.back()">
        <ArrowLeft :size="20" />
      </button>
      <h1 class="text-lg font-semibold">物流追踪</h1>
    </header>

    <div v-if="loading" class="px-5 py-10 text-center text-muted-foreground text-sm">加载中…</div>

    <div v-else class="px-5 pt-4 pb-8 space-y-3">
      <div
        v-for="s in shipments"
        :key="s.id"
        class="bg-card rounded-xl p-4 shadow-sm cursor-pointer"
        @click="toggle(s.id)"
      >
        <div class="flex items-center gap-3">
          <div class="w-9 h-9 rounded-lg bg-primary-soft flex items-center justify-center flex-none">
            <Truck :size="18" class="text-primary-soft-fg" />
          </div>
          <div class="flex-1 min-w-0">
            <p class="text-sm font-medium truncate">{{ s.productName }}</p>
            <p class="text-[11px] text-muted-foreground mt-0.5">{{ s.carrier }} · {{ s.trackingNo }}</p>
          </div>
          <span :class="['text-[11px] px-2 py-0.5 rounded-full flex-none', statusMeta[s.status].cls]">
            {{ statusMeta[s.status].label }}
          </span>
        </div>

        <!-- Traces -->
        <div v-if="activeId === s.id" class="mt-4 pt-3 border-t border-border">
          <p class="text-[11px] text-muted-foreground mb-2">关联合同 {{ s.contractCode }}</p>
          <div class="space-y-0">
            <div v-for="(t, idx) in s.traces" :key="idx" class="flex gap-3">
              <div class="flex flex-col items-center flex-none">
                <span :class="['w-2 h-2 rounded-full mt-1.5', idx === 0 ? 'bg-primary' : 'bg-border']" />
                <span v-if="idx < s.traces.length - 1" class="w-px flex-1 bg-border my-0.5" />
              </div>
              <div class="pb-3 min-w-0">
                <p class="text-xs">{{ t.desc }}</p>
                <p class="text-[10px] text-muted-foreground mt-0.5">{{ t.time }}<template v-if="t.location"> · {{ t.location }}</template></p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
