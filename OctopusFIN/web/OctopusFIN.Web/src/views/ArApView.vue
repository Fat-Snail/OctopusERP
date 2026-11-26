<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { Receipt, Wallet } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Modal from '@/components/ui/Modal.vue'
import { getArList, getApList, registerReceipt, registerPayment, type ArInvoice, type ApInvoice } from '@/api'

const tab = ref<'ar' | 'ap'>('ar')
const arRows = ref<ArInvoice[]>([])
const apRows = ref<ApInvoice[]>([])
const loading = ref(false)
const settleOpen = ref(false)
const settleTarget = ref<ArInvoice | ApInvoice | null>(null)
const settleForm = ref({ amount: 0, paymentMethod: '银行转账', bankReference: '' })
const error = ref('')

const statusLabels: Record<string, string> = { open: '未结清', partial: '部分结清', settled: '已结清', closed: '已关闭' }
const outstandingAr = computed(() => arRows.value.reduce((s, r) => s + (r.amount - r.receivedAmount), 0))
const outstandingAp = computed(() => apRows.value.reduce((s, r) => s + (r.amount - r.paidAmount), 0))

async function load() {
  loading.value = true
  try {
    const [ar, ap] = await Promise.all([getArList().catch(() => []), getApList().catch(() => [])])
    arRows.value = ar
    apRows.value = ap
  } finally { loading.value = false }
}

function openSettle(row: ArInvoice | ApInvoice) {
  const remaining = tab.value === 'ar' ? row.amount - (row as ArInvoice).receivedAmount : row.amount - (row as ApInvoice).paidAmount
  settleTarget.value = row
  settleForm.value = { amount: Number(remaining.toFixed(2)), paymentMethod: '银行转账', bankReference: '' }
  error.value = ''
  settleOpen.value = true
}

async function submitSettle() {
  if (!settleTarget.value || settleForm.value.amount <= 0) { error.value = '金额必须大于 0'; return }
  try {
    if (tab.value === 'ar') await registerReceipt(settleTarget.value.invoiceId, settleForm.value)
    else await registerPayment(settleTarget.value.invoiceId, settleForm.value)
    settleOpen.value = false
    await load()
  } catch (e) { error.value = (e as Error).message }
}

function fmt(v: number) { return Number(v ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2 }) }
onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="应收应付" sub="客户应收与供应商应付的发票及收付款登记">
      <template #tabs>
        <div class="flex gap-1 -mb-px">
          <button :class="['px-4 py-2 text-[12px] border-b-2 cursor-pointer', tab === 'ar' ? 'border-primary text-primary font-medium' : 'border-transparent text-muted-foreground']" @click="tab = 'ar'">应收账款</button>
          <button :class="['px-4 py-2 text-[12px] border-b-2 cursor-pointer', tab === 'ap' ? 'border-primary text-primary font-medium' : 'border-transparent text-muted-foreground']" @click="tab = 'ap'">应付账款</button>
        </div>
      </template>
    </PageHeader>
    <div class="p-5">
      <div class="grid grid-cols-2 gap-4 mb-4">
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
          <div class="text-[12px] text-muted-foreground">应收未结余额</div>
          <div class="text-[22px] font-semibold font-mono-tnum text-foreground mt-1">¥ {{ fmt(outstandingAr) }}</div>
        </div>
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
          <div class="text-[12px] text-muted-foreground">应付未结余额</div>
          <div class="text-[22px] font-semibold font-mono-tnum text-foreground mt-1">¥ {{ fmt(outstandingAp) }}</div>
        </div>
      </div>

      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">发票号</th>
            <th class="text-left px-4 py-2.5 font-medium">{{ tab === 'ar' ? '客户' : '供应商' }}</th>
            <th class="text-right px-4 py-2.5 font-medium">金额</th>
            <th class="text-right px-4 py-2.5 font-medium">{{ tab === 'ar' ? '已收' : '已付' }}</th>
            <th class="text-right px-4 py-2.5 font-medium">未结</th>
            <th class="text-left px-4 py-2.5 font-medium">开票日期</th>
            <th class="text-left px-4 py-2.5 font-medium">状态</th>
            <th class="text-right px-4 py-2.5 font-medium">操作</th>
          </tr></thead>
          <tbody>
            <template v-if="tab === 'ar'">
              <tr v-for="r in arRows" :key="r.invoiceId" class="border-t border-border hover:bg-[var(--subtle)]">
                <td class="px-4 py-2.5 font-mono-tnum">{{ r.invoiceNo }}</td>
                <td class="px-4 py-2.5">{{ r.customerName }}</td>
                <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.amount) }}</td>
                <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.receivedAmount) }}</td>
                <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.amount - r.receivedAmount) }}</td>
                <td class="px-4 py-2.5 text-muted-foreground">{{ r.invoiceDate?.slice(0, 10) }}</td>
                <td class="px-4 py-2.5"><StatusBadge :value="r.status" :labels="statusLabels" /></td>
                <td class="px-4 py-2.5 text-right"><button v-if="r.status !== 'settled' && r.status !== 'closed'" class="inline-flex items-center gap-1 px-2 py-1 text-[11px] border border-border rounded bg-card cursor-pointer" @click="openSettle(r)"><Receipt :size="12" /> 登记收款</button><span v-else class="text-muted-foreground">—</span></td>
              </tr>
            </template>
            <template v-else>
              <tr v-for="r in apRows" :key="r.invoiceId" class="border-t border-border hover:bg-[var(--subtle)]">
                <td class="px-4 py-2.5 font-mono-tnum">{{ r.invoiceNo }}</td>
                <td class="px-4 py-2.5">{{ r.supplierName }}</td>
                <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.amount) }}</td>
                <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.paidAmount) }}</td>
                <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.amount - r.paidAmount) }}</td>
                <td class="px-4 py-2.5 text-muted-foreground">{{ r.invoiceDate?.slice(0, 10) }}</td>
                <td class="px-4 py-2.5"><StatusBadge :value="r.status" :labels="statusLabels" /></td>
                <td class="px-4 py-2.5 text-right"><button v-if="r.status !== 'settled' && r.status !== 'closed'" class="inline-flex items-center gap-1 px-2 py-1 text-[11px] border border-border rounded bg-card cursor-pointer" @click="openSettle(r)"><Wallet :size="12" /> 登记付款</button><span v-else class="text-muted-foreground">—</span></td>
              </tr>
            </template>
            <tr v-if="loading || (tab === 'ar' ? arRows.length === 0 : apRows.length === 0)"><td colspan="8" class="text-center py-8 text-muted-foreground">{{ loading ? '加载中...' : '暂无数据' }}</td></tr>
          </tbody>
        </table>
      </div>
    </div>

    <Modal :open="settleOpen" :title="tab === 'ar' ? '登记收款' : '登记付款'" @close="settleOpen = false">
      <div v-if="settleTarget" class="space-y-3">
        <div class="text-[12px] p-2 bg-muted rounded">{{ settleTarget.invoiceNo }} · 金额 {{ fmt(settleTarget.amount) }}</div>
        <label class="block text-[12px]">金额<input v-model.number="settleForm.amount" type="number" min="0" step="0.01" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">收付方式<input v-model="settleForm.paymentMethod" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">银行流水号<input v-model="settleForm.bankReference" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <p v-if="error" class="text-[12px] text-danger">{{ error }}</p>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="settleOpen = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="submitSettle">确认登记</button>
      </template>
    </Modal>
  </div>
</template>
