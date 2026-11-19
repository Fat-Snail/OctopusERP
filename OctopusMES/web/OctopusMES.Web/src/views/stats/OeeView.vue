<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Gauge, RefreshCw } from '@lucide/vue'
import { getOee } from '@/api/stats'
import type { OeeResponse } from '@/api/stats/types'
import PageHeader from '@/components/app/PageHeader.vue'

const data = ref<OeeResponse | null>(null)
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    data.value = await getOee()
  } finally {
    loading.value = false
  }
}

onMounted(load)

const pct = (v: number, digits = 1) => `${(v * 100).toFixed(digits)}%`

const oeeBarColor = (oee: number) =>
  oee >= 0.85 ? 'bg-success' : oee >= 0.65 ? 'bg-warning' : 'bg-danger'

const oeeGrade = (oee: number) =>
  oee >= 0.85 ? '优秀' : oee >= 0.65 ? '良好' : oee >= 0.4 ? '待改进' : '无数据/偏低'

const statusMeta: Record<string, { label: string; cls: string }> = {
  running: { label: '运行中', cls: 'text-success bg-success/10' },
  idle: { label: '空闲', cls: 'text-muted-foreground bg-muted' },
  maintenance: { label: '维护中', cls: 'text-warning bg-warning/10' },
  fault: { label: '故障', cls: 'text-danger bg-danger/10' },
  offline: { label: '离线', cls: 'text-muted-foreground bg-muted' },
}
</script>

<template>
  <div class="p-5 space-y-4 max-w-6xl mx-auto">
    <PageHeader title="设备 OEE 看板" description="可用率 × 表现率 × 质量率（报工口径，Q3-④）">
      <template #actions>
        <button
          class="flex items-center gap-1.5 h-8 px-3 rounded-lg border border-border bg-card text-xs cursor-pointer hover:bg-muted"
          :disabled="loading"
          @click="load"
        >
          <RefreshCw :size="13" :class="loading ? 'animate-spin' : ''" /> 刷新
        </button>
      </template>
    </PageHeader>

    <div v-if="loading && !data" class="py-20 text-center text-muted-foreground text-sm">加载中…</div>

    <template v-else-if="data">
      <!-- Overall -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-3">
        <div class="bg-card rounded-xl p-5 shadow-sm border border-border col-span-1 md:row-span-2 flex flex-col items-center justify-center">
          <div class="w-10 h-10 rounded-xl bg-primary-soft flex items-center justify-center mb-2">
            <Gauge :size="20" class="text-primary-soft-fg" />
          </div>
          <p class="text-xs text-muted-foreground">全局 OEE</p>
          <p class="text-4xl font-bold mt-1" :class="oeeBarColor(data.overall.oee).replace('bg-', 'text-')">
            {{ pct(data.overall.oee) }}
          </p>
          <p class="text-[11px] text-muted-foreground mt-1">{{ oeeGrade(data.overall.oee) }}</p>
        </div>
        <div class="bg-card rounded-xl p-4 shadow-sm border border-border">
          <p class="text-xs text-muted-foreground">可用率 Availability</p>
          <p class="text-2xl font-semibold mt-1">{{ pct(data.overall.availability) }}</p>
          <p class="text-[11px] text-muted-foreground mt-1">1 - 停机时长 / 报工窗口</p>
        </div>
        <div class="bg-card rounded-xl p-4 shadow-sm border border-border">
          <p class="text-xs text-muted-foreground">表现率 Performance</p>
          <p class="text-2xl font-semibold mt-1">{{ pct(data.overall.performance) }}</p>
          <p class="text-[11px] text-muted-foreground mt-1">Σ产量×标准节拍 / Σ产量×实际节拍（≤100%）</p>
        </div>
        <div class="bg-card rounded-xl p-4 shadow-sm border border-border">
          <p class="text-xs text-muted-foreground">质量率 Quality</p>
          <p class="text-2xl font-semibold mt-1">{{ pct(data.overall.quality) }}</p>
          <p class="text-[11px] text-muted-foreground mt-1">合格数 / 总产量</p>
        </div>
      </div>

      <!-- Per equipment -->
      <div class="bg-card rounded-xl border border-border shadow-sm overflow-hidden">
        <div class="px-4 py-3 border-b border-border">
          <h2 class="text-sm font-semibold">分设备明细</h2>
        </div>
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="text-left text-[11px] text-muted-foreground border-b border-border bg-muted/50">
                <th class="px-4 py-2.5 font-medium">设备</th>
                <th class="px-4 py-2.5 font-medium">工作中心</th>
                <th class="px-4 py-2.5 font-medium">状态</th>
                <th class="px-4 py-2.5 font-medium">OEE</th>
                <th class="px-4 py-2.5 font-medium">可用率</th>
                <th class="px-4 py-2.5 font-medium">表现率</th>
                <th class="px-4 py-2.5 font-medium">质量率</th>
                <th class="px-4 py-2.5 font-medium text-right">运行/停机</th>
                <th class="px-4 py-2.5 font-medium text-right">产量(合格/总)</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="eq in data.equipments"
                :key="eq.equipmentId"
                class="border-b border-border/60 last:border-0 hover:bg-muted/30"
              >
                <td class="px-4 py-3">
                  <p class="font-medium">{{ eq.equipmentName }}</p>
                  <p class="text-[11px] text-muted-foreground font-mono">{{ eq.equipmentCode }}</p>
                </td>
                <td class="px-4 py-3 text-xs text-muted-foreground">{{ eq.workCenter ?? '—' }}</td>
                <td class="px-4 py-3">
                  <span :class="['text-[11px] px-2 py-0.5 rounded-full', (statusMeta[eq.status] ?? statusMeta.offline).cls]">
                    {{ (statusMeta[eq.status] ?? statusMeta.offline).label }}
                  </span>
                </td>
                <td class="px-4 py-3">
                  <div class="flex items-center gap-2 min-w-[120px]">
                    <span class="font-semibold w-14" :class="oeeBarColor(eq.oee).replace('bg-', 'text-')">
                      {{ pct(eq.oee) }}
                    </span>
                    <div class="flex-1 h-1.5 rounded-full bg-muted overflow-hidden">
                      <div class="h-full rounded-full" :class="oeeBarColor(eq.oee)" :style="{ width: Math.min(100, eq.oee * 100) + '%' }" />
                    </div>
                  </div>
                </td>
                <td class="px-4 py-3">{{ pct(eq.availability) }}</td>
                <td class="px-4 py-3">{{ pct(eq.performance) }}</td>
                <td class="px-4 py-3">{{ pct(eq.quality) }}</td>
                <td class="px-4 py-3 text-right text-xs font-mono whitespace-nowrap">
                  {{ eq.runMinutes }} / {{ eq.downtimeMinutes }} 分
                </td>
                <td class="px-4 py-3 text-right text-xs font-mono whitespace-nowrap">
                  {{ eq.qualifiedQty.toLocaleString('zh-CN') }} / {{ eq.totalQty.toLocaleString('zh-CN') }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <p class="text-[11px] text-muted-foreground leading-relaxed">{{ data.note }}</p>
    </template>
  </div>
</template>
