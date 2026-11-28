<script setup lang="ts">
import { ref, onMounted } from 'vue'
import PageHeader from '@/components/app/PageHeader.vue'
import {
  getPeriods, getTrialBalance, getMonthEndClosing,
  type Period, type TrialBalanceResponse, type MonthEndClosingReport,
} from '@/api'

const periods = ref<Period[]>([])
const periodId = ref<number | undefined>()
const tb = ref<TrialBalanceResponse | null>(null)
const loading = ref(false)
const closing = ref<MonthEndClosingReport | null>(null)

async function load() {
  loading.value = true
  try { tb.value = await getTrialBalance(periodId.value) } catch { tb.value = null } finally { loading.value = false }
}

async function loadClosing() {
  if (!periodId.value) { closing.value = null; return }
  try { closing.value = await getMonthEndClosing(periodId.value) } catch { closing.value = null }
}

function fmt(v: number | null | undefined) {
  return Number(v ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function dirLabel(d: string) { return d === 'debit' ? '借' : '贷' }
function statusLabel(s: string) { return s === 'closed' ? '已关账' : '进行中' }
function statusClass(s: string) {
  return s === 'closed'
    ? 'bg-[color-mix(in_oklch,var(--success)_16%,transparent)] text-[var(--success)]'
    : 'bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)]'
}
function fmtTime(t: string | null | undefined) {
  if (!t) return '—'
  const d = new Date(t)
  if (isNaN(d.getTime())) return String(t)
  return d.toLocaleString('zh-CN', { hour12: false })
}

onMounted(async () => {
  try {
    periods.value = await getPeriods()
    periodId.value = periods.value.find(p => p.status === 'open')?.periodId ?? periods.value[0]?.periodId
    await Promise.all([load(), loadClosing()])
  } catch { /* ignore */ }
})
</script>

<template>
  <div>
    <PageHeader title="财务报表" sub="试算平衡表与月结报告" />
    <div class="p-5 space-y-5">
      <!-- 过滤条 -->
      <div class="flex items-center gap-3">
        <span class="text-[12px] text-muted-foreground">会计期间</span>
        <select v-model.number="periodId" class="h-8 px-2 border border-border rounded bg-card text-[12px]" @change="load(); loadClosing()">
          <option :value="undefined">全部期间</option>
          <option v-for="p in periods" :key="p.periodId" :value="p.periodId">{{ p.periodCode }}</option>
        </select>
        <button class="h-8 px-3 border border-border rounded bg-card text-[12px] cursor-pointer" @click="load(); loadClosing()">刷新</button>
      </div>

      <!-- 试算平衡表 -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <div class="flex items-center justify-between px-4 py-3 border-b border-border">
          <div class="text-[13px] font-semibold">试算平衡表</div>
          <span v-if="tb"
            :class="['text-[11px] px-2 py-0.5 rounded-full', tb.isBalanced ? 'bg-[color-mix(in_oklch,var(--success)_16%,transparent)] text-[var(--success)]' : 'bg-[color-mix(in_oklch,var(--danger)_16%,transparent)] text-[var(--danger)]']">
            {{ tb.isBalanced ? '借贷平衡' : '借贷不平' }}
          </span>
        </div>
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground">
            <tr>
              <th class="text-left px-4 py-2 font-medium">科目编码</th>
              <th class="text-left px-4 py-2 font-medium">科目名称</th>
              <th class="text-center px-4 py-2 font-medium">方向</th>
              <th class="text-right px-4 py-2 font-medium">借方发生</th>
              <th class="text-right px-4 py-2 font-medium">贷方发生</th>
              <th class="text-right px-4 py-2 font-medium">余额</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loading"><td colspan="6" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="!tb || tb.rows.length === 0"><td colspan="6" class="text-center py-8 text-muted-foreground">暂无数据</td></tr>
            <tr v-for="(r, i) in (tb?.rows ?? [])" :key="i" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2 font-mono-tnum">{{ r.accountCode }}</td>
              <td class="px-4 py-2">{{ r.accountName }}</td>
              <td class="px-4 py-2 text-center text-muted-foreground">{{ dirLabel(r.direction) }}</td>
              <td class="px-4 py-2 text-right font-mono-tnum">{{ Number(r.debitTotal) ? fmt(r.debitTotal) : '' }}</td>
              <td class="px-4 py-2 text-right font-mono-tnum">{{ Number(r.creditTotal) ? fmt(r.creditTotal) : '' }}</td>
              <td class="px-4 py-2 text-right font-mono-tnum font-medium">{{ fmt(r.balance) }}</td>
            </tr>
          </tbody>
          <tfoot v-if="tb && tb.rows.length" class="border-t-2 border-border font-medium bg-[var(--subtle)]">
            <tr>
              <td colspan="3" class="px-4 py-2 text-right">合计</td>
              <td class="px-4 py-2 text-right font-mono-tnum">{{ fmt(tb.totalDebit) }}</td>
              <td class="px-4 py-2 text-right font-mono-tnum">{{ fmt(tb.totalCredit) }}</td>
              <td class="px-4 py-2"></td>
            </tr>
          </tfoot>
        </table>
      </div>

      <!-- 月结报告 -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <div class="flex items-center justify-between px-4 py-3 border-b border-border">
          <div class="text-[13px] font-semibold">月结报告</div>
          <span v-if="closing" :class="['text-[11px] px-2 py-0.5 rounded-full', statusClass(closing.periodStatus)]">
            {{ closing.periodCode }} · {{ statusLabel(closing.periodStatus) }}
          </span>
        </div>

        <div v-if="!closing" class="px-4 py-6 text-[12px] text-muted-foreground">
          选择一个具体会计期间以查看月结报告。
        </div>

        <template v-else>
          <!-- 统计卡片 -->
          <div class="grid grid-cols-2 md:grid-cols-4 gap-px bg-border">
            <div class="bg-card p-4">
              <div class="text-[11px] text-muted-foreground mb-1">凭证统计</div>
              <div class="text-[18px] font-semibold font-mono-tnum">{{ closing.voucherTotal }}</div>
              <div class="text-[11px] text-muted-foreground mt-1">已过账 {{ closing.postedCount }} · 草稿 {{ closing.draftCount }} · 作废 {{ closing.voidCount }}</div>
            </div>
            <div class="bg-card p-4">
              <div class="text-[11px] text-muted-foreground mb-1">本期发生额</div>
              <div class="text-[18px] font-semibold font-mono-tnum">{{ fmt(closing.postedDebit) }}</div>
              <div class="text-[11px] text-muted-foreground mt-1">借方 · 贷方 {{ fmt(closing.postedCredit) }}</div>
            </div>
            <div class="bg-card p-4">
              <div class="text-[11px] text-muted-foreground mb-1">收入 / 费用</div>
              <div class="text-[18px] font-semibold font-mono-tnum">{{ fmt(closing.revenueTotal) }}</div>
              <div class="text-[11px] text-muted-foreground mt-1">费用 {{ fmt(closing.expenseTotal) }}</div>
            </div>
            <div class="bg-card p-4">
              <div class="text-[11px] text-muted-foreground mb-1">净利润</div>
              <div :class="['text-[18px] font-semibold font-mono-tnum', Number(closing.netProfit) >= 0 ? 'text-[var(--success)]' : 'text-[var(--danger)]']">
                {{ fmt(closing.netProfit) }}
              </div>
              <div class="text-[11px] text-muted-foreground mt-1">
                {{ closing.trialBalanceOk ? '试算平衡 ✓' : '试算不平 ✗' }}
              </div>
            </div>
          </div>

          <!-- 明细行 -->
          <div class="border-t border-border px-4 py-3 flex flex-wrap gap-x-8 gap-y-2 text-[12px]">
            <div><span class="text-muted-foreground">关账时间：</span>{{ fmtTime(closing.closedAt) }}</div>
            <div><span class="text-muted-foreground">关账人：</span>{{ closing.closedBy ?? '—' }}</div>
            <div>
              <span class="text-muted-foreground">结转凭证：</span>
              {{ closing.closingVoucherNo ?? '未结转' }}
              <span v-if="closing.closingAmount != null" class="text-muted-foreground">（{{ fmt(closing.closingAmount) }}）</span>
            </div>
          </div>

          <!-- 科目余额快照 -->
          <div class="border-t border-border">
            <div class="px-4 py-2.5 text-[12px] font-medium text-muted-foreground bg-[var(--subtle)]">科目余额快照</div>
            <table class="w-full text-[12px]">
              <thead class="bg-muted text-muted-foreground">
                <tr>
                  <th class="text-left px-4 py-2 font-medium">科目编码</th>
                  <th class="text-left px-4 py-2 font-medium">科目名称</th>
                  <th class="text-center px-4 py-2 font-medium">方向</th>
                  <th class="text-right px-4 py-2 font-medium">借方发生</th>
                  <th class="text-right px-4 py-2 font-medium">贷方发生</th>
                  <th class="text-right px-4 py-2 font-medium">余额</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="closing.balanceSnapshot.length === 0"><td colspan="6" class="text-center py-6 text-muted-foreground">暂无数据</td></tr>
                <tr v-for="(r, i) in closing.balanceSnapshot" :key="i" class="border-t border-border hover:bg-[var(--subtle)]">
                  <td class="px-4 py-2 font-mono-tnum">{{ r.accountCode }}</td>
                  <td class="px-4 py-2">{{ r.accountName }}</td>
                  <td class="px-4 py-2 text-center text-muted-foreground">{{ dirLabel(r.direction) }}</td>
                  <td class="px-4 py-2 text-right font-mono-tnum">{{ Number(r.debitTotal) ? fmt(r.debitTotal) : '' }}</td>
                  <td class="px-4 py-2 text-right font-mono-tnum">{{ Number(r.creditTotal) ? fmt(r.creditTotal) : '' }}</td>
                  <td class="px-4 py-2 text-right font-mono-tnum font-medium">{{ fmt(r.balance) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>