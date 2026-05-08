<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { TrendingUp, Clock, Target, AlertTriangle, CheckCircle, RefreshCw } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import {
  getBiEfficiency,
  getOtdTrend,
  getApprovalBacklog,
  getContractTimeline,
  getContractBriefList,
  getPipeline,
} from '@/api/stats'
import type {
  BiEfficiencyResponse,
  OtdTrendPoint,
  ApprovalBacklogItem,
  ContractTimelineResponse,
  ContractBriefRow,
  PipelineStageItem,
} from '@/api/stats/types'

const loading = ref(false)
const efficiency = ref<BiEfficiencyResponse | null>(null)
const otdTrend = ref<OtdTrendPoint[]>([])
const backlog = ref<ApprovalBacklogItem[]>([])
const pipeline = ref<PipelineStageItem[]>([])
const contracts = ref<ContractBriefRow[]>([])
const selectedContractId = ref<number | null>(null)
const timeline = ref<ContractTimelineResponse | null>(null)
const timelineLoading = ref(false)

const STATUS_LABELS: Record<string, string> = {
  draft: '草稿',
  pending_approval: '审批中',
  active: '执行中',
  shipped: '已发货',
  completed: '已完成',
  terminated: '已终止',
}

// ─── KPI 定义（指标、SLA 目标、显示格式）────────────────────────────────────
interface KpiDef {
  key: keyof BiEfficiencyResponse
  label: string
  sla: number
  unit: string
  format: (v: number) => string
  good: (v: number, sla: number) => boolean
}

const kpis: KpiDef[] = [
  {
    key: 'inquiryToQuoteHours',
    label: '询盘→报价时效',
    sla: 8,
    unit: 'h',
    format: v => `${v.toFixed(1)}h`,
    good: (v, sla) => v <= sla,
  },
  {
    key: 'quoteApprovalDays',
    label: '报价审批时效',
    sla: 2,
    unit: 'd',
    format: v => `${v.toFixed(1)}天`,
    good: (v, sla) => v <= sla,
  },
  {
    key: 'contractCycleDays',
    label: '合同签署周期',
    sla: 7,
    unit: 'd',
    format: v => `${v.toFixed(1)}天`,
    good: (v, sla) => v <= sla,
  },
  {
    key: 'otdRate',
    label: '客户侧 OTD',
    sla: 0.95,
    unit: '%',
    format: v => v === 0 ? '—' : `${(v * 100).toFixed(1)}%`,
    good: (v, sla) => v === 0 || v >= sla,
  },
  {
    key: 'supplierDeliveryRate',
    label: '供应侧交期达成',
    sla: 0.9,
    unit: '%',
    format: v => `${(v * 100).toFixed(1)}%`,
    good: (v, sla) => v >= sla,
  },
  {
    key: 'approvalOverdueRate',
    label: '审批超时率',
    sla: 0.05,
    unit: '%',
    format: v => `${(v * 100).toFixed(1)}%`,
    good: (v, sla) => v <= sla,
  },
  {
    key: 'dso',
    label: 'DSO 应收账期',
    sla: 30,
    unit: 'd',
    format: v => v === 0 ? '—' : `${v.toFixed(1)}天`,
    good: (v, sla) => v === 0 || v <= sla,
  },
]

function kpiStatus(kpi: KpiDef, val: number): 'green' | 'yellow' | 'red' {
  if (kpi.good(val, kpi.sla)) return 'green'
  // within 20% of SLA threshold
  const slack = Math.abs(kpi.sla) * 0.2
  if (kpi.good(val + slack, kpi.sla)) return 'yellow'
  return 'red'
}

function slaLabel(kpi: KpiDef): string {
  if (kpi.unit === '%') return `SLA ${(kpi.sla * 100).toFixed(0)}%`
  return `SLA ${kpi.sla}${kpi.unit}`
}

// ─── OTD 趋势折线图（SVG）────────────────────────────────────────────────────
const SVG_W = 520
const SVG_H = 100
const CHART_L = 40
const CHART_R = SVG_W - 10
const CHART_T = 10
const CHART_B = SVG_H - 20
const CHART_W = CHART_R - CHART_L
const CHART_H = CHART_B - CHART_T

function toY(rate: number): number {
  return CHART_T + CHART_H * (1 - rate)
}
function toX(idx: number, total: number): number {
  if (total <= 1) return CHART_L + CHART_W / 2
  return CHART_L + (idx / (total - 1)) * CHART_W
}

const otdPolylinePoints = computed(() => {
  const pts = otdTrend.value
  if (!pts.length) return ''
  return pts
    .map((p, i) => `${toX(i, pts.length).toFixed(1)},${toY(p.total > 0 ? p.otdRate : -1).toFixed(1)}`)
    .filter((_, i) => otdTrend.value[i].total > 0)
    .join(' ')
})

const slaLineY = computed(() => toY(0.95).toFixed(1))

const hasOtdData = computed(() => otdTrend.value.some(p => p.total > 0))

// ─── 销售漏斗 ─────────────────────────────────────────────────────────────────
const funnelStages = computed(() => {
  const maxCount = Math.max(...pipeline.value.map(p => p.count), 1)
  return pipeline.value.map(p => ({
    ...p,
    widthPct: Math.max(20, (p.count / maxCount) * 100),
    label: p.stage === 'inquiry' ? '询盘' : p.stage === 'quote' ? '报价' : '合同',
  }))
})

// ─── 审批积压最大值（用于柱宽比例）─────────────────────────────────────────
const backlogMax = computed(() => Math.max(...backlog.value.map(b => b.pendingCount), 1))

// ─── 合同时间轴 ───────────────────────────────────────────────────────────────
const STAGE_ICONS = ['📩', '📋', '✅', '📝', '🚚', '💰']

async function loadTimeline(id: number) {
  timelineLoading.value = true
  try {
    timeline.value = await getContractTimeline(id)
  } finally {
    timelineLoading.value = false
  }
}

watch(selectedContractId, id => {
  if (id !== null) loadTimeline(id)
})

async function loadAll() {
  loading.value = true
  try {
    ;[efficiency.value, otdTrend.value, backlog.value, pipeline.value, contracts.value] =
      await Promise.all([
        getBiEfficiency(),
        getOtdTrend(6),
        getApprovalBacklog(),
        getPipeline(),
        getContractBriefList(),
      ])
    if (contracts.value.length > 0 && selectedContractId.value === null) {
      selectedContractId.value = contracts.value[0].contractId
    }
  } finally {
    loading.value = false
  }
}

onMounted(loadAll)
</script>

<template>
  <div>
    <PageHeader title="全链路 BI 看板" sub="销售漏斗 · 时效仪表盘 · OTD 趋势 · 审批积压 · 合同时间轴">
      <template #actions>
        <button
          class="flex items-center gap-1.5 h-8 px-3 rounded-[var(--radius)] border border-border text-[12px] text-muted-foreground hover:text-foreground hover:bg-muted transition-colors cursor-pointer bg-transparent"
          :disabled="loading"
          @click="loadAll"
        >
          <RefreshCw :size="13" :class="loading ? 'animate-spin' : ''" />
          刷新
        </button>
      </template>
    </PageHeader>

    <div class="p-5 space-y-5">

      <!-- Row 1: 销售漏斗 + 时效仪表盘 -->
      <div class="grid grid-cols-[280px_1fr] gap-5">

        <!-- 销售漏斗 -->
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
          <div class="flex items-center gap-2 mb-4">
            <TrendingUp :size="14" class="text-[var(--info)]" />
            <span class="text-[13px] font-semibold">销售漏斗</span>
          </div>
          <div class="space-y-3">
            <div v-for="(stage, i) in funnelStages" :key="stage.stage" class="space-y-1">
              <div class="flex items-center justify-between text-[11px]">
                <span class="text-muted-foreground">{{ stage.label }}</span>
                <span class="font-mono-tnum font-medium">{{ stage.count }}</span>
              </div>
              <div class="h-6 bg-muted rounded overflow-hidden flex items-center">
                <div
                  class="h-full rounded transition-all duration-500"
                  :style="{ width: `${stage.widthPct}%` }"
                  :class="i === 0 ? 'bg-[var(--info)]' : i === 1 ? 'bg-[var(--primary)]' : 'bg-[var(--success)]'"
                />
              </div>
              <div v-if="stage.amount > 0" class="text-[10px] text-muted-foreground font-mono-tnum">
                ¥{{ stage.amount.toLocaleString('zh-CN') }}
              </div>
              <!-- conversion arrow -->
              <div v-if="i < funnelStages.length - 1 && funnelStages[i].count > 0" class="text-center text-[10px] text-muted-foreground">
                ↓ 转化率 {{ funnelStages[i + 1].count > 0 ? ((funnelStages[i + 1].count / funnelStages[i].count) * 100).toFixed(0) : 0 }}%
              </div>
            </div>
          </div>
        </div>

        <!-- 时效仪表盘 -->
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
          <div class="flex items-center gap-2 mb-4">
            <Clock :size="14" class="text-[var(--warning)]" />
            <span class="text-[13px] font-semibold">时效仪表盘</span>
            <span class="ml-auto text-[10px] text-muted-foreground">对比 SLA 目标</span>
          </div>

          <div v-if="loading" class="space-y-3">
            <div v-for="i in 7" :key="i" class="h-9 bg-muted rounded animate-pulse" />
          </div>

          <div v-else-if="efficiency" class="divide-y divide-border">
            <div
              v-for="kpi in kpis"
              :key="kpi.key"
              class="flex items-center gap-3 py-2.5"
            >
              <!-- status dot -->
              <div
                class="w-2 h-2 rounded-full shrink-0"
                :class="{
                  'bg-[var(--success)]': kpiStatus(kpi, efficiency[kpi.key]) === 'green',
                  'bg-[var(--warning)]': kpiStatus(kpi, efficiency[kpi.key]) === 'yellow',
                  'bg-[var(--danger)]': kpiStatus(kpi, efficiency[kpi.key]) === 'red',
                }"
              />
              <span class="text-[12px] text-foreground w-[110px] shrink-0">{{ kpi.label }}</span>
              <!-- value -->
              <span
                class="text-[13px] font-semibold font-mono-tnum w-[70px]"
                :class="{
                  'text-[var(--success)]': kpiStatus(kpi, efficiency[kpi.key]) === 'green',
                  'text-[var(--warning)]': kpiStatus(kpi, efficiency[kpi.key]) === 'yellow',
                  'text-[var(--danger)]': kpiStatus(kpi, efficiency[kpi.key]) === 'red',
                }"
              >
                {{ kpi.format(efficiency[kpi.key]) }}
              </span>
              <!-- progress bar -->
              <div class="flex-1 h-1.5 bg-muted rounded-full overflow-hidden">
                <div
                  class="h-full rounded-full transition-all duration-500"
                  :class="{
                    'bg-[var(--success)]': kpiStatus(kpi, efficiency[kpi.key]) === 'green',
                    'bg-[var(--warning)]': kpiStatus(kpi, efficiency[kpi.key]) === 'yellow',
                    'bg-[var(--danger)]': kpiStatus(kpi, efficiency[kpi.key]) === 'red',
                  }"
                  :style="{
                    width: kpi.unit === '%'
                      ? `${Math.min(100, efficiency[kpi.key] / kpi.sla * 100).toFixed(0)}%`
                      : `${Math.min(100, efficiency[kpi.key] / Math.max(kpi.sla * 2, 0.01) * 100).toFixed(0)}%`
                  }"
                />
              </div>
              <span class="text-[10px] text-muted-foreground w-[60px] text-right shrink-0">{{ slaLabel(kpi) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Row 2: OTD 趋势 + 审批积压 -->
      <div class="grid grid-cols-[1fr_280px] gap-5">

        <!-- OTD 趋势折线图 -->
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
          <div class="flex items-center gap-2 mb-4">
            <Target :size="14" class="text-[var(--success)]" />
            <span class="text-[13px] font-semibold">OTD 趋势（近 6 个月）</span>
            <span class="ml-auto flex items-center gap-3 text-[10px] text-muted-foreground">
              <span class="flex items-center gap-1">
                <span class="inline-block w-6 h-0.5 bg-[var(--primary)]" />实际 OTD
              </span>
              <span class="flex items-center gap-1">
                <span class="inline-block w-6 h-0.5 border-t border-dashed border-[var(--warning)]" />SLA 95%
              </span>
            </span>
          </div>

          <div v-if="loading" class="h-[120px] bg-muted rounded animate-pulse" />

          <template v-else>
            <div v-if="!hasOtdData" class="h-[120px] flex flex-col items-center justify-center gap-2">
              <CheckCircle :size="24" class="text-muted-foreground/40" />
              <p class="text-[12px] text-muted-foreground">暂无已发货合同记录</p>
              <p class="text-[11px] text-muted-foreground/60">合同完成发货后此处将显示趋势数据</p>
            </div>

            <div v-else class="relative">
              <svg :viewBox="`0 0 ${SVG_W} ${SVG_H}`" class="w-full" :height="SVG_H">
                <!-- Y 轴刻度线 -->
                <line :x1="CHART_L" :y1="toY(1)" :x2="CHART_R" :y2="toY(1)"
                  stroke="currentColor" stroke-opacity="0.08" />
                <line :x1="CHART_L" :y1="toY(0.5)" :x2="CHART_R" :y2="toY(0.5)"
                  stroke="currentColor" stroke-opacity="0.08" />
                <line :x1="CHART_L" :y1="toY(0)" :x2="CHART_R" :y2="toY(0)"
                  stroke="currentColor" stroke-opacity="0.08" />

                <!-- Y 轴标签 -->
                <text :x="CHART_L - 4" :y="toY(1) + 4" text-anchor="end"
                  font-size="9" fill="currentColor" opacity="0.5">100%</text>
                <text :x="CHART_L - 4" :y="toY(0.5) + 4" text-anchor="end"
                  font-size="9" fill="currentColor" opacity="0.5">50%</text>
                <text :x="CHART_L - 4" :y="toY(0) + 4" text-anchor="end"
                  font-size="9" fill="currentColor" opacity="0.5">0%</text>

                <!-- SLA 95% 线 -->
                <line :x1="CHART_L" :y1="slaLineY" :x2="CHART_R" :y2="slaLineY"
                  stroke="oklch(0.72 0.16 75)" stroke-width="1" stroke-dasharray="4,3" opacity="0.7" />
                <text :x="CHART_R + 2" :y="slaLineY" font-size="8"
                  fill="oklch(0.72 0.16 75)" opacity="0.9" dominant-baseline="middle">95%</text>

                <!-- OTD 折线 -->
                <polyline
                  v-if="otdPolylinePoints"
                  :points="otdPolylinePoints"
                  fill="none"
                  stroke="oklch(0.55 0.18 250)"
                  stroke-width="2"
                  stroke-linejoin="round"
                  stroke-linecap="round"
                />

                <!-- 数据点 -->
                <template v-for="(pt, i) in otdTrend" :key="pt.month">
                  <circle
                    v-if="pt.total > 0"
                    :cx="toX(i, otdTrend.length)"
                    :cy="toY(pt.otdRate)"
                    r="3"
                    fill="oklch(0.55 0.18 250)"
                    stroke="white"
                    stroke-width="1.5"
                  />
                </template>

                <!-- X 轴月份标签 -->
                <text
                  v-for="(pt, i) in otdTrend"
                  :key="`label-${pt.month}`"
                  :x="toX(i, otdTrend.length)"
                  :y="SVG_H - 4"
                  text-anchor="middle"
                  font-size="9"
                  fill="currentColor"
                  opacity="0.5"
                >{{ pt.month.slice(5) }}月</text>
              </svg>
            </div>

            <!-- 月度明细 -->
            <div class="mt-3 grid grid-cols-6 gap-1">
              <div
                v-for="pt in otdTrend"
                :key="pt.month"
                class="text-center"
              >
                <div class="text-[10px] text-muted-foreground">{{ pt.month.slice(5) }}月</div>
                <div class="text-[11px] font-mono-tnum mt-0.5"
                  :class="pt.total === 0 ? 'text-muted-foreground/50' : pt.otdRate >= 0.95 ? 'text-[var(--success)]' : 'text-[var(--danger)]'">
                  {{ pt.total === 0 ? '—' : `${(pt.otdRate * 100).toFixed(0)}%` }}
                </div>
                <div class="text-[9px] text-muted-foreground/60">{{ pt.total > 0 ? `${pt.onTime}/${pt.total}` : '无记录' }}</div>
              </div>
            </div>
          </template>
        </div>

        <!-- 审批积压 -->
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
          <div class="flex items-center gap-2 mb-4">
            <AlertTriangle :size="14" class="text-[var(--danger)]" />
            <span class="text-[13px] font-semibold">审批积压</span>
          </div>

          <div v-if="loading" class="space-y-4">
            <div v-for="i in 3" :key="i" class="h-12 bg-muted rounded animate-pulse" />
          </div>

          <div v-else class="space-y-4">
            <div
              v-for="item in backlog"
              :key="item.stage"
              class="space-y-1.5"
            >
              <div class="flex items-center justify-between text-[11px]">
                <span class="text-muted-foreground">{{ item.label }}</span>
                <span class="font-mono-tnum">
                  <span class="text-foreground font-medium">{{ item.pendingCount }}</span>
                  <span v-if="item.overdueCount > 0" class="text-[var(--danger)] ml-1">
                    （{{ item.overdueCount }} 超时）
                  </span>
                </span>
              </div>
              <div class="h-2 bg-muted rounded-full overflow-hidden">
                <div
                  class="h-full rounded-full transition-all duration-500"
                  :class="item.overdueCount > 0 ? 'bg-[var(--danger)]' : item.pendingCount > 0 ? 'bg-[var(--warning)]' : 'bg-[var(--success)]'"
                  :style="{ width: `${Math.max(4, (item.pendingCount / backlogMax) * 100).toFixed(0)}%` }"
                />
              </div>
              <div class="text-[10px] text-muted-foreground/60">
                {{ item.pendingCount === 0 ? '无积压' : `${item.pendingCount} 条待处理，SLA 2 工作日` }}
              </div>
            </div>

            <div class="pt-2 border-t border-border">
              <div class="flex items-center justify-between text-[11px]">
                <span class="text-muted-foreground">总待处理</span>
                <span class="font-mono-tnum font-medium">
                  {{ backlog.reduce((s, b) => s + b.pendingCount, 0) }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Row 3: 合同全链路时间轴 -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
        <div class="flex items-center gap-3 mb-4">
          <CheckCircle :size="14" class="text-[var(--primary-soft-fg)]" />
          <span class="text-[13px] font-semibold">合同全链路时间轴</span>

          <!-- 合同选择器 -->
          <select
            v-model="selectedContractId"
            class="ml-auto h-7 px-2 rounded-[var(--radius)] border border-border bg-card text-[12px] text-foreground focus:outline-none focus:ring-1 focus:ring-primary/30 cursor-pointer"
          >
            <option v-if="contracts.length === 0" :value="null" disabled>暂无合同</option>
            <option
              v-for="c in contracts"
              :key="c.contractId"
              :value="c.contractId"
            >
              {{ c.contractCode }} · {{ c.customerName }} · {{ STATUS_LABELS[c.status] ?? c.status }}
            </option>
          </select>
        </div>

        <div v-if="timelineLoading || loading" class="h-24 bg-muted rounded animate-pulse" />

        <div v-else-if="!timeline" class="h-24 flex items-center justify-center text-[12px] text-muted-foreground">
          请选择合同查看时间轴
        </div>

        <template v-else>
          <!-- Timeline header -->
          <div class="text-[11px] text-muted-foreground mb-4">
            {{ timeline.customerName }} · {{ timeline.contractCode }}
          </div>

          <!-- Steps -->
          <div class="flex items-start gap-0">
            <template v-for="(evt, i) in timeline.events" :key="evt.stage">
              <!-- Step node -->
              <div class="flex flex-col items-center" style="flex: 1">
                <!-- Circle + label row -->
                <div class="flex flex-col items-center">
                  <div
                    class="w-8 h-8 rounded-full flex items-center justify-center text-[14px] transition-colors border-2"
                    :class="{
                      'bg-[color-mix(in_oklch,var(--success)_15%,transparent)] border-[var(--success)]': evt.isCompleted,
                      'bg-[color-mix(in_oklch,var(--info)_12%,transparent)] border-[var(--info)]': !evt.isCompleted && i === timeline.events.findIndex(e => !e.isCompleted),
                      'bg-muted border-border': !evt.isCompleted && i !== timeline.events.findIndex(e => !e.isCompleted),
                    }"
                  >
                    {{ STAGE_ICONS[i] }}
                  </div>
                  <div class="text-[11px] font-medium mt-1.5 text-center leading-tight"
                    :class="evt.isCompleted ? 'text-foreground' : 'text-muted-foreground'">
                    {{ evt.label }}
                  </div>
                  <div class="text-[10px] text-muted-foreground mt-0.5 text-center font-mono-tnum">
                    {{ evt.eventTime ? new Date(evt.eventTime).toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' }) : '—' }}
                  </div>
                  <div v-if="evt.remark" class="text-[9px] mt-0.5 text-center px-1"
                    :class="evt.remark === '逾期交付' ? 'text-[var(--danger)]' : 'text-[var(--success)]'">
                    {{ evt.remark }}
                  </div>
                </div>
              </div>

              <!-- Connector line -->
              <div
                v-if="i < timeline.events.length - 1"
                class="mt-4 h-0.5 w-6 shrink-0"
                :class="evt.isCompleted && timeline.events[i + 1].isCompleted ? 'bg-[var(--success)]' : 'bg-border'"
              />
            </template>
          </div>
        </template>
      </div>

    </div>
  </div>
</template>
