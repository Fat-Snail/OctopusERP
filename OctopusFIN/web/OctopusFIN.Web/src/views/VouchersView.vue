<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Plus, Search, Eye, CheckCircle, Ban } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Modal from '@/components/ui/Modal.vue'
import {
  getVouchers, getVoucher, createVoucher, postVoucher, voidVoucher, getAccounts, getPeriods,
  type Voucher, type Account, type Period, type VoucherEntryInput,
} from '@/api'

const rows = ref<Voucher[]>([])
const total = ref(0)
const loading = ref(false)
const pageNum = ref(1)
const pageSize = 20
const statusFilter = ref('')
const sourceFilter = ref('')
const detail = ref<Voucher | null>(null)
const detailOpen = ref(false)

const createOpen = ref(false)
const accounts = ref<Account[]>([])
const periods = ref<Period[]>([])
const form = ref({
  periodId: 0 as number | undefined,
  voucherDate: new Date().toISOString().slice(0, 10),
  voucherType: 'normal',
  summary: '',
  entries: [
    { accountId: 0, summary: '', debitAmount: 0, creditAmount: 0 },
    { accountId: 0, summary: '', debitAmount: 0, creditAmount: 0 },
  ] as VoucherEntryInput[],
})
const formError = ref('')

const totalDebit = computed(() => form.value.entries.reduce((s, e) => s + Number(e.debitAmount || 0), 0))
const totalCredit = computed(() => form.value.entries.reduce((s, e) => s + Number(e.creditAmount || 0), 0))
const balanced = computed(() => Math.abs(totalDebit.value - totalCredit.value) < 0.005 && totalDebit.value > 0)

const statusLabels: Record<string, string> = { draft: '草稿', posted: '已过账', void: '已作废' }
const sourceLabels: Record<string, string> = { manual: '手工', oa: 'OA', mes: 'MES', crm: 'CRM', hrm: 'HRM' }

async function loadData() {
  loading.value = true
  try {
    const res = await getVouchers({ pageNum: pageNum.value, pageSize, status: statusFilter.value || undefined, source: sourceFilter.value || undefined })
    rows.value = res.rows
    total.value = res.total
  } catch { rows.value = [] } finally { loading.value = false }
}

async function openDetail(id: number) {
  try { detail.value = await getVoucher(id); detailOpen.value = true } catch { /* ignore */ }
}

async function openCreate() {
  formError.value = ''
  form.value = {
    periodId: periods.value.find(p => p.status === 'open')?.periodId,
    voucherDate: new Date().toISOString().slice(0, 10),
    voucherType: 'normal',
    summary: '',
    entries: [
      { accountId: accounts.value[0]?.accountId ?? 0, summary: '', debitAmount: 0, creditAmount: 0 },
      { accountId: accounts.value[1]?.accountId ?? accounts.value[0]?.accountId ?? 0, summary: '', debitAmount: 0, creditAmount: 0 },
    ],
  }
  createOpen.value = true
}

function addEntry() { form.value.entries.push({ accountId: accounts.value[0]?.accountId ?? 0, summary: '', debitAmount: 0, creditAmount: 0 }) }
function removeEntry(idx: number) { if (form.value.entries.length > 2) form.value.entries.splice(idx, 1) }

async function submitCreate() {
  if (!balanced.value) { formError.value = '借贷必须平衡且金额大于 0'; return }
  if (form.value.entries.some(e => !e.accountId)) { formError.value = '请选择每条分录的科目'; return }
  try {
    await createVoucher({
      periodId: form.value.periodId,
      voucherDate: form.value.voucherDate,
      voucherType: form.value.voucherType,
      summary: form.value.summary,
      entries: form.value.entries.map(e => ({
        accountId: e.accountId,
        summary: e.summary,
        debitAmount: Number(e.debitAmount || 0),
        creditAmount: Number(e.creditAmount || 0),
      })),
    })
    createOpen.value = false
    await loadData()
  } catch (e) { formError.value = (e as Error).message }
}

async function doPost(id: number) { try { await postVoucher(id); await loadData() } catch { /* ignore */ } }
async function doVoid(id: number) { try { await voidVoucher(id); await loadData() } catch { /* ignore */ } }
function fmt(v: number) { return Number(v ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2 }) }

onMounted(async () => {
  await Promise.all([loadData(), getAccounts().then(r => accounts.value = r).catch(() => {}), getPeriods().then(r => periods.value = r).catch(() => {})])
})
</script>

<template>
  <div>
    <PageHeader title="凭证管理" sub="复式记账凭证的录入、过账与作废">
      <template #actions>
        <button class="flex items-center gap-1.5 h-8 px-3 bg-primary text-primary-foreground rounded-[var(--radius)] text-[12px] font-medium border-0 cursor-pointer" @click="openCreate">
          <Plus :size="14" /> 录入凭证
        </button>
      </template>
    </PageHeader>

    <div class="p-5">
      <div class="flex items-center gap-3 mb-4">
        <input v-model="statusFilter" placeholder="状态（draft/posted/void）" class="h-8 px-3 border border-border rounded-[var(--radius)] bg-card text-[12px] w-48" @keyup.enter="loadData" />
        <input v-model="sourceFilter" placeholder="来源（manual/oa/mes...）" class="h-8 px-3 border border-border rounded-[var(--radius)] bg-card text-[12px] w-48" @keyup.enter="loadData" />
        <button class="flex items-center gap-1 h-8 px-3 border border-border rounded-[var(--radius)] bg-card text-[12px] cursor-pointer" @click="loadData"><Search :size="13" /> 查询</button>
        <span class="ml-auto text-[11px] text-muted-foreground">共 {{ total }} 条</span>
      </div>

      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground">
            <tr>
              <th class="text-left px-4 py-2.5 font-medium">凭证号</th>
              <th class="text-left px-4 py-2.5 font-medium">日期</th>
              <th class="text-left px-4 py-2.5 font-medium">摘要</th>
              <th class="text-right px-4 py-2.5 font-medium">借方合计</th>
              <th class="text-right px-4 py-2.5 font-medium">贷方合计</th>
              <th class="text-left px-4 py-2.5 font-medium">状态</th>
              <th class="text-left px-4 py-2.5 font-medium">来源</th>
              <th class="text-right px-4 py-2.5 font-medium">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loading"><td colspan="8" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="rows.length === 0"><td colspan="8" class="text-center py-8 text-muted-foreground">暂无凭证</td></tr>
            <tr v-for="r in rows" :key="r.voucherId" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2.5 font-mono-tnum">{{ r.voucherNo }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.voucherDate?.slice(0, 10) }}</td>
              <td class="px-4 py-2.5 max-w-[240px] truncate">{{ r.summary || '—' }}</td>
              <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.totalDebit) }}</td>
              <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.totalCredit) }}</td>
              <td class="px-4 py-2.5"><StatusBadge :value="r.status" :labels="statusLabels" /></td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ sourceLabels[r.source] ?? r.source }}</td>
              <td class="px-4 py-2.5">
                <div class="flex items-center justify-end gap-1">
                  <button title="查看分录" class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-muted cursor-pointer border-0 bg-transparent" @click="openDetail(r.voucherId)"><Eye :size="14" /></button>
                  <button v-if="r.status === 'draft'" title="过账" class="p-1 rounded text-muted-foreground hover:text-[var(--success)] hover:bg-muted cursor-pointer border-0 bg-transparent" @click="doPost(r.voucherId)"><CheckCircle :size="14" /></button>
                  <button v-if="r.status === 'draft'" title="作废" class="p-1 rounded text-muted-foreground hover:text-danger hover:bg-muted cursor-pointer border-0 bg-transparent" @click="doVoid(r.voucherId)"><Ban :size="14" /></button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
        <div class="flex items-center justify-between px-4 py-2.5 border-t border-border text-[11px] text-muted-foreground">
          <span>第 {{ pageNum }} 页</span>
          <div class="flex gap-2">
            <button :disabled="pageNum <= 1" class="px-2 py-1 border border-border rounded disabled:opacity-40 cursor-pointer bg-card" @click="pageNum--; loadData()">上一页</button>
            <button :disabled="pageNum * pageSize >= total" class="px-2 py-1 border border-border rounded disabled:opacity-40 cursor-pointer bg-card" @click="pageNum++; loadData()">下一页</button>
          </div>
        </div>
      </div>
    </div>

    <!-- 详情 -->
    <Modal :open="detailOpen" :title="`凭证 ${detail?.voucherNo ?? ''}`" @close="detailOpen = false">
      <div v-if="detail" class="space-y-3">
        <div class="grid grid-cols-2 gap-3 text-[12px]">
          <div><span class="text-muted-foreground">日期：</span>{{ detail.voucherDate?.slice(0, 10) }}</div>
          <div><span class="text-muted-foreground">状态：</span><StatusBadge :value="detail.status" :labels="statusLabels" /></div>
          <div class="col-span-2"><span class="text-muted-foreground">摘要：</span>{{ detail.summary || '—' }}</div>
        </div>
        <table class="w-full text-[12px] border border-border rounded">
          <thead class="bg-muted text-muted-foreground"><tr><th class="text-left px-2 py-1.5">科目</th><th class="text-left px-2 py-1.5">摘要</th><th class="text-right px-2 py-1.5">借方</th><th class="text-right px-2 py-1.5">贷方</th></tr></thead>
          <tbody>
            <tr v-for="e in detail.entries" :key="e.entryId" class="border-t border-border">
              <td class="px-2 py-1.5">{{ e.accountCode }} {{ e.accountName }}</td>
              <td class="px-2 py-1.5 text-muted-foreground">{{ e.summary || '—' }}</td>
              <td class="px-2 py-1.5 text-right font-mono-tnum">{{ Number(e.debitAmount) ? fmt(e.debitAmount) : '' }}</td>
              <td class="px-2 py-1.5 text-right font-mono-tnum">{{ Number(e.creditAmount) ? fmt(e.creditAmount) : '' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </Modal>

    <!-- 新建 -->
    <Modal :open="createOpen" title="录入凭证" width="max-w-[760px]" @close="createOpen = false">
      <div class="space-y-4">
        <div class="grid grid-cols-3 gap-3">
          <label class="block text-[12px]">会计期间
            <select v-model.number="form.periodId" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card">
              <option :value="undefined">自动</option>
              <option v-for="p in periods" :key="p.periodId" :value="p.periodId">{{ p.periodCode }}</option>
            </select>
          </label>
          <label class="block text-[12px]">凭证日期
            <input v-model="form.voucherDate" type="date" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" />
          </label>
          <label class="block text-[12px]">凭证类型
            <input v-model="form.voucherType" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" />
          </label>
        </div>
        <label class="block text-[12px]">摘要
          <input v-model="form.summary" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" />
        </label>
        <div>
          <div class="flex items-center justify-between mb-2">
            <span class="text-[12px] font-medium">分录明细</span>
            <button class="text-[12px] text-primary cursor-pointer border-0 bg-transparent" @click="addEntry">+ 添加分录</button>
          </div>
          <table class="w-full text-[12px]">
            <thead class="bg-muted text-muted-foreground"><tr><th class="text-left px-2 py-1.5">科目</th><th class="text-left px-2 py-1.5">摘要</th><th class="text-right px-2 py-1.5">借方</th><th class="text-right px-2 py-1.5">贷方</th><th></th></tr></thead>
            <tbody>
              <tr v-for="(e, idx) in form.entries" :key="idx" class="border-t border-border">
                <td class="px-1 py-1.5">
                  <select v-model.number="e.accountId" class="w-full h-8 px-1 border border-border rounded bg-card text-[11px]">
                    <option :value="0">选择科目</option>
                    <option v-for="a in accounts" :key="a.accountId" :value="a.accountId">{{ a.accountCode }} {{ a.accountName }}</option>
                  </select>
                </td>
                <td class="px-1 py-1.5"><input v-model="e.summary" class="w-full h-8 px-2 border border-border rounded bg-card text-[11px]" /></td>
                <td class="px-1 py-1.5"><input v-model.number="e.debitAmount" type="number" min="0" step="0.01" class="w-24 h-8 px-2 border border-border rounded bg-card text-right text-[11px]" /></td>
                <td class="px-1 py-1.5"><input v-model.number="e.creditAmount" type="number" min="0" step="0.01" class="w-24 h-8 px-2 border border-border rounded bg-card text-right text-[11px]" /></td>
                <td class="px-1 py-1.5"><button class="text-danger text-[16px] leading-none border-0 bg-transparent cursor-pointer" @click="removeEntry(idx)">×</button></td>
              </tr>
            </tbody>
          </table>
          <div class="flex justify-end gap-6 mt-2 text-[12px]">
            <span :class="balanced ? 'text-[var(--success)]' : 'text-danger'">借方 {{ fmt(totalDebit) }} / 贷方 {{ fmt(totalCredit) }} {{ balanced ? '✓ 平衡' : '（不平衡）' }}</span>
          </div>
        </div>
        <p v-if="formError" class="text-[12px] text-danger">{{ formError }}</p>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="createOpen = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="submitCreate">保存凭证</button>
      </template>
    </Modal>
  </div>
</template>
