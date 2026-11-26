<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import PageHeader from '@/components/app/PageHeader.vue'
import { getPeriods, getTrialBalance, getMonthEndClosing, type Period, type TrialBalanceRow } from '@/api'

const periods = ref<Period[]>([])
const periodId = ref<number | undefined>()
const rows = ref<TrialBalanceRow[]>([])
const loading = ref(false)
const closing = ref<unknown>(null)

const totalDebit = computed(() => rows.value.reduce((s, r) => s + Number(r.endDebit || 0), 0))
const totalCredit = computed(() => rows.value.reduce((s, r) => s + Number(r.endCredit || 0), 0))

async function load() {
  loading.value = true
  try { rows.value = await getTrialBalance(periodId.value) } catch { rows.value = [] } finally { loading.value = false }
}

async function loadClosing() {
  if (!periodId.value) return
  try { closing.value = await getMonthEndClosing(periodId.value) } catch { closing.value = null }
}

function fmt(v: number) { return Number(v ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2 }) }

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
      <div class="flex items-center gap-3">
        <span class="text-[12px] text-muted-foreground">会计期间</span>
        <select v-model.number="periodId" class="h-8 px-2 border border-border rounded bg-card text-[12px]" @change="load(); loadClosing()">
          <option :value="undefined">全部期间</option>
          <option v-for="p in periods" :key="p.periodId" :value="p.periodId">{{ p.periodCode }}</option>
        </select>
        <button class="h-8 px-3 border border-border rounded bg-card text-[12px] cursor-pointer" @click="load(); loadClosing()">刷新</button>
      </div>

      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <div class="px-4 py-3 border-b border-border text-[13px] font-semibold">试算平衡表</div>
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2 font-medium">科目编码</th><th class="text-left px-4 py-2 font-medium">科目名称</th>
            <th class="text-right px-4 py-2 font-medium">期初借</th><th class="text-right px-4 py-2 font-medium">期初贷</th>
            <th class="text-right px-4 py-2 font-medium">本期借</th><th class="text-right px-4 py-2 font-medium">本期贷</th>
            <th class="text-right px-4 py-2 font-medium">期末借</th><th class="text-right px-4 py-2 font-medium">期末贷</th>
          </tr></thead>
          <tbody>
            <tr v-if="loading"><td colspan="8" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="rows.length === 0"><td colspan="8" class="text-center py-8 text-muted-foreground">暂无数据</td></tr>
            <tr v-for="(r, i) in rows" :key="i" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2 font-mono-tnum">{{ r.accountCode }}</td>
              <td class="px-4 py-2">{{ r.accountName }}</td>
              <td class="px-4 py-2 text-right font-mono-tnum">{{ Number(r.beginDebit) ? fmt(r.beginDebit) : '' }}</td>
              <td class="px-4 py-2 text-right font-mono-tnum">{{ Number(r.beginCredit) ? fmt(r.beginCredit) : '' }}</td>
              <td class="px-4 py-2 text-right font-mono-tnum">{{ Number(r.periodDebit) ? fmt(r.periodDebit) : '' }}</td>
              <td class="px-4 py-2 text-right font-mono-tnum">{{ Number(r.periodCredit) ? fmt(r.periodCredit) : '' }}</td>
              <td class="px-4 py-2 text-right font-mono-tnum">{{ Number(r.endDebit) ? fmt(r.endDebit) : '' }}</td>
              <td class="px-4 py-2 text-right font-mono-tnum">{{ Number(r.endCredit) ? fmt(r.endCredit) : '' }}</td>
            </tr>
          </tbody>
          <tfoot v-if="rows.length" class="border-t-2 border-border font-medium">
            <tr><td colspan="6" class="px-4 py-2 text-right">合计</td><td class="px-4 py-2 text-right font-mono-tnum">{{ fmt(totalDebit) }}</td><td class="px-4 py-2 text-right font-mono-tnum">{{ fmt(totalCredit) }}</td></tr>
          </tfoot>
        </table>
      </div>

      <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
        <div class="text-[13px] font-semibold mb-3">月结报告</div>
        <pre v-if="closing" class="text-[11px] text-muted-foreground whitespace-pre-wrap font-mono">{{ JSON.stringify(closing, null, 2) }}</pre>
        <p v-else class="text-[12px] text-muted-foreground">选择一个期间以查看月结报告。</p>
      </div>
    </div>
  </div>
</template>
