<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Search, Eye, Wallet, Warehouse, Ban, Truck } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Modal from '@/components/ui/Modal.vue'
import { getOrders, getOrder, getShops, payOrder, pushWms, cancelOrder, pushShipment, type Order, type Shop } from '@/api'

const rows = ref<Order[]>([])
const shops = ref<Shop[]>([])
const total = ref(0)
const loading = ref(false)
const pageNum = ref(1)
const pageSize = 20
const statusFilter = ref('')
const shopFilter = ref<number | undefined>()
const detail = ref<Order | null>(null)
const detailOpen = ref(false)

const payOpen = ref(false)
const payForm = ref({ id: 0, amount: 0 })
const cancelOpen = ref(false)
const cancelForm = ref({ id: 0, reason: '' })
const shipOpen = ref(false)
const shipForm = ref({ id: 0, trackingNo: '', carrier: '顺丰速运' })
const message = ref('')

const statusLabels: Record<string, string> = {
  created: '已创建', paid: '已支付', processing: '处理中', shipped: '已发货', completed: '已完成', cancelled: '已取消', refunded: '已退款',
}

async function load() {
  loading.value = true
  try {
    const res = await getOrders({ pageNum: pageNum.value, pageSize, status: statusFilter.value || undefined, shopId: shopFilter.value })
    rows.value = res.rows
    total.value = res.total
  } catch { rows.value = [] } finally { loading.value = false }
}

function shopName(id: number) { return shops.value.find(s => s.shopId === id)?.shopName ?? `#${id}` }

async function openDetail(id: number) { try { detail.value = await getOrder(id); detailOpen.value = true } catch { /* ignore */ } }

async function doPay() {
  try { await payOrder(payForm.value.id, Number(payForm.value.amount)); payOpen.value = false; await load() }
  catch (e) { message.value = (e as Error).message }
}
async function doPushWms(id: number) { try { await pushWms(id); await load() } catch (e) { message.value = (e as Error).message } }
async function doCancel() { try { await cancelOrder(cancelForm.value.id, cancelForm.value.reason); cancelOpen.value = false; await load() } catch (e) { message.value = (e as Error).message } }
async function doShip() { try { await pushShipment(shipForm.value.id, shipForm.value.trackingNo, shipForm.value.carrier); shipOpen.value = false; await load() } catch (e) { message.value = (e as Error).message } }
function fmt(v: number) { return Number(v ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2 }) }

onMounted(async () => {
  try { shops.value = await getShops() } catch { /* ignore */ }
  await load()
})
</script>

<template>
  <div>
    <PageHeader title="订单管理" sub="渠道订单的查询、支付、下推 WMS 与发货" />
    <div class="p-5">
      <p v-if="message" class="mb-4 text-[12px] text-danger">{{ message }}</p>
      <div class="flex items-center gap-3 mb-4">
        <select v-model.number="shopFilter" class="h-8 px-2 border border-border rounded bg-card text-[12px] min-w-[160px]">
          <option :value="undefined">全部店铺</option>
          <option v-for="s in shops" :key="s.shopId" :value="s.shopId">{{ s.shopName }}</option>
        </select>
        <input v-model="statusFilter" placeholder="状态（paid/shipped...）" class="h-8 px-3 border border-border rounded bg-card text-[12px] w-48" @keyup.enter="load" />
        <button class="flex items-center gap-1 h-8 px-3 border border-border rounded bg-card text-[12px] cursor-pointer" @click="load"><Search :size="13" /> 查询</button>
        <span class="ml-auto text-[11px] text-muted-foreground">共 {{ total }} 条</span>
      </div>

      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">订单号</th><th class="text-left px-4 py-2.5 font-medium">平台订单号</th>
            <th class="text-left px-4 py-2.5 font-medium">店铺</th><th class="text-left px-4 py-2.5 font-medium">客户</th>
            <th class="text-right px-4 py-2.5 font-medium">订单金额</th><th class="text-left px-4 py-2.5 font-medium">状态</th>
            <th class="text-left px-4 py-2.5 font-medium">下单时间</th><th class="text-right px-4 py-2.5 font-medium">操作</th>
          </tr></thead>
          <tbody>
            <tr v-if="loading"><td colspan="8" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="rows.length === 0"><td colspan="8" class="text-center py-8 text-muted-foreground">暂无订单</td></tr>
            <tr v-for="r in rows" :key="r.orderId" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2.5 font-mono-tnum">{{ r.orderNo }}</td>
              <td class="px-4 py-2.5 font-mono-tnum text-muted-foreground">{{ r.externalOrderNo }}</td>
              <td class="px-4 py-2.5">{{ shopName(r.shopId) }}</td>
              <td class="px-4 py-2.5">{{ r.customerName || '—' }}</td>
              <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.totalAmount) }}</td>
              <td class="px-4 py-2.5"><StatusBadge :value="r.status" :labels="statusLabels" /></td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.orderDate?.slice(0, 16) }}</td>
              <td class="px-4 py-2.5">
                <div class="flex items-center justify-end gap-1">
                  <button title="详情" class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-muted cursor-pointer border-0 bg-transparent" @click="openDetail(r.orderId)"><Eye :size="14" /></button>
                  <button v-if="r.status === 'created'" title="标记支付" class="p-1 rounded text-muted-foreground hover:text-[var(--success)] hover:bg-muted cursor-pointer border-0 bg-transparent" @click="payForm = { id: r.orderId, amount: Number(r.totalAmount) }; payOpen = true"><Wallet :size="14" /></button>
                  <button v-if="r.status === 'paid'" title="下推 WMS" class="p-1 rounded text-muted-foreground hover:text-primary hover:bg-muted cursor-pointer border-0 bg-transparent" @click="doPushWms(r.orderId)"><Warehouse :size="14" /></button>
                  <button v-if="r.status === 'processing'" title="发货回传" class="p-1 rounded text-muted-foreground hover:text-primary hover:bg-muted cursor-pointer border-0 bg-transparent" @click="shipForm = { id: r.orderId, trackingNo: '', carrier: '顺丰速运' }; shipOpen = true"><Truck :size="14" /></button>
                  <button v-if="['created', 'paid'].includes(r.status)" title="取消" class="p-1 rounded text-muted-foreground hover:text-danger hover:bg-muted cursor-pointer border-0 bg-transparent" @click="cancelForm = { id: r.orderId, reason: '' }; cancelOpen = true"><Ban :size="14" /></button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
        <div class="flex items-center justify-between px-4 py-2.5 border-t border-border text-[11px] text-muted-foreground">
          <span>第 {{ pageNum }} 页</span>
          <div class="flex gap-2">
            <button :disabled="pageNum <= 1" class="px-2 py-1 border border-border rounded disabled:opacity-40 cursor-pointer bg-card" @click="pageNum--; load()">上一页</button>
            <button :disabled="pageNum * pageSize >= total" class="px-2 py-1 border border-border rounded disabled:opacity-40 cursor-pointer bg-card" @click="pageNum++; load()">下一页</button>
          </div>
        </div>
      </div>
    </div>

    <!-- 详情 -->
    <Modal :open="detailOpen" :title="`订单 ${detail?.orderNo ?? ''}`" width="max-w-[720px]" @close="detailOpen = false">
      <div v-if="detail" class="space-y-4">
        <div class="grid grid-cols-2 gap-3 text-[12px]">
          <div><span class="text-muted-foreground">平台订单号：</span>{{ detail.externalOrderNo }}</div>
          <div><span class="text-muted-foreground">状态：</span><StatusBadge :value="detail.status" :labels="statusLabels" /></div>
          <div><span class="text-muted-foreground">客户：</span>{{ detail.customerName || '—' }}</div>
          <div><span class="text-muted-foreground">金额：</span>¥ {{ fmt(detail.totalAmount) }} / 已付 ¥ {{ fmt(detail.paidAmount) }}</div>
        </div>
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr><th class="text-left px-2 py-1.5">SKU</th><th class="text-left px-2 py-1.5">商品</th><th class="text-right px-2 py-1.5">数量</th><th class="text-right px-2 py-1.5">单价</th><th class="text-right px-2 py-1.5">小计</th></tr></thead>
          <tbody>
            <tr v-for="it in detail.items ?? []" :key="it.itemId" class="border-t border-border">
              <td class="px-2 py-1.5 font-mono-tnum">{{ it.channelSkuCode }}</td><td class="px-2 py-1.5">{{ it.productName }}</td>
              <td class="px-2 py-1.5 text-right font-mono-tnum">{{ it.quantity }}</td><td class="px-2 py-1.5 text-right font-mono-tnum">{{ fmt(it.unitPrice) }}</td>
              <td class="px-2 py-1.5 text-right font-mono-tnum">{{ fmt(it.amount) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </Modal>

    <Modal :open="payOpen" title="标记支付" @close="payOpen = false">
      <label class="block text-[12px]">支付金额<input v-model.number="payForm.amount" type="number" min="0" step="0.01" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="payOpen = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="doPay">确认</button>
      </template>
    </Modal>

    <Modal :open="cancelOpen" title="取消订单" @close="cancelOpen = false">
      <label class="block text-[12px]">取消原因<input v-model="cancelForm.reason" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="cancelOpen = false">返回</button>
        <button class="px-3 py-1.5 bg-destructive text-destructive-foreground rounded text-[12px] cursor-pointer border-0" @click="doCancel">确认取消</button>
      </template>
    </Modal>

    <Modal :open="shipOpen" title="发货回传" @close="shipOpen = false">
      <div class="space-y-3">
        <label class="block text-[12px]">快递单号<input v-model="shipForm.trackingNo" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">快递公司<input v-model="shipForm.carrier" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="shipOpen = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="doShip">回传</button>
      </template>
    </Modal>
  </div>
</template>
