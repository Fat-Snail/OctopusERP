<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Plus, CheckCircle, CircleCheck } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Modal from '@/components/ui/Modal.vue'
import { getAfterSales, createAfterSale, approveAfterSale, completeAfterSale, type AfterSale } from '@/api'

const rows = ref<AfterSale[]>([])
const loading = ref(false)
const statusFilter = ref('')
const open = ref(false)
const error = ref('')
const form = ref({ orderId: 0, type: 'refund', amount: 0, reason: '', note: '' })

const statusLabels: Record<string, string> = { pending: '待审批', approved: '已批准', rejected: '已驳回', completed: '已完成' }
const typeLabels: Record<string, string> = { refund: '退款', return: '退货', exchange: '换货', supplement: '补发' }

async function load() {
  loading.value = true
  try { rows.value = await getAfterSales(statusFilter.value || undefined) } catch { rows.value = [] } finally { loading.value = false }
}
async function submit() {
  error.value = ''
  if (!form.value.orderId || form.value.amount <= 0) { error.value = '请填写订单 ID 和金额'; return }
  try {
    await createAfterSale(form.value)
    open.value = false
    form.value = { orderId: 0, type: 'refund', amount: 0, reason: '', note: '' }
    await load()
  } catch (e) { error.value = (e as Error).message }
}
async function doApprove(r: AfterSale) { try { await approveAfterSale(r.afterSaleId, ''); await load() } catch (e) { error.value = (e as Error).message } }
async function doComplete(r: AfterSale) { try { await completeAfterSale(r.afterSaleId); await load() } catch (e) { error.value = (e as Error).message } }
function fmt(v: number) { return Number(v ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2 }) }
onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="售后管理" sub="退款/退货/换货/补发工单的审批与完成">
      <template #actions>
        <button class="flex items-center gap-1.5 h-8 px-3 bg-primary text-primary-foreground rounded-[var(--radius)] text-[12px] font-medium border-0 cursor-pointer" @click="open = true"><Plus :size="14" /> 发起售后</button>
      </template>
    </PageHeader>
    <div class="p-5">
      <p v-if="error" class="mb-3 text-[12px] text-danger">{{ error }}</p>
      <div class="flex items-center gap-3 mb-4">
        <select v-model="statusFilter" class="h-8 px-2 border border-border rounded bg-card text-[12px]" @change="load">
          <option value="">全部状态</option><option value="pending">待审批</option><option value="approved">已批准</option><option value="completed">已完成</option>
        </select>
        <span class="ml-auto text-[11px] text-muted-foreground">共 {{ rows.length }} 条</span>
      </div>
      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">售后单号</th><th class="text-left px-4 py-2.5 font-medium">订单 ID</th>
            <th class="text-left px-4 py-2.5 font-medium">类型</th><th class="text-right px-4 py-2.5 font-medium">金额</th>
            <th class="text-left px-4 py-2.5 font-medium">原因</th><th class="text-left px-4 py-2.5 font-medium">状态</th>
            <th class="text-left px-4 py-2.5 font-medium">创建时间</th><th class="text-right px-4 py-2.5 font-medium">操作</th>
          </tr></thead>
          <tbody>
            <tr v-if="loading"><td colspan="8" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="rows.length === 0"><td colspan="8" class="text-center py-8 text-muted-foreground">暂无售后单</td></tr>
            <tr v-for="r in rows" :key="r.afterSaleId" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2.5 font-mono-tnum">{{ r.afterSaleNo }}</td><td class="px-4 py-2.5 font-mono-tnum">{{ r.orderId }}</td>
              <td class="px-4 py-2.5">{{ typeLabels[r.afterSaleType] ?? r.afterSaleType }}</td>
              <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.amount) }}</td>
              <td class="px-4 py-2.5 max-w-[200px] truncate text-muted-foreground">{{ r.reason || '—' }}</td>
              <td class="px-4 py-2.5"><StatusBadge :value="r.status" :labels="statusLabels" /></td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.createdAt?.slice(0, 16) }}</td>
              <td class="px-4 py-2.5">
                <div class="flex items-center justify-end gap-1">
                  <button v-if="r.status === 'pending'" class="inline-flex items-center gap-1 px-2 py-1 text-[11px] border border-border rounded bg-card cursor-pointer" @click="doApprove(r)"><CheckCircle :size="12" /> 批准</button>
                  <button v-if="r.status === 'approved'" class="inline-flex items-center gap-1 px-2 py-1 text-[11px] bg-primary text-primary-foreground rounded border-0 cursor-pointer" @click="doComplete(r)"><CircleCheck :size="12" /> 完成</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <Modal :open="open" title="发起售后" @close="open = false">
      <div class="space-y-3">
        <div class="grid grid-cols-2 gap-3">
          <label class="block text-[12px]">订单 ID<input v-model.number="form.orderId" type="number" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
          <label class="block text-[12px]">售后类型<select v-model="form.type" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card"><option v-for="(v, k) in typeLabels" :key="k" :value="k">{{ v }}</option></select></label>
        </div>
        <label class="block text-[12px]">金额<input v-model.number="form.amount" type="number" min="0" step="0.01" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">原因<input v-model="form.reason" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">备注<input v-model="form.note" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <p v-if="error" class="text-[12px] text-danger">{{ error }}</p>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="open = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="submit">提交</button>
      </template>
    </Modal>
  </div>
</template>
