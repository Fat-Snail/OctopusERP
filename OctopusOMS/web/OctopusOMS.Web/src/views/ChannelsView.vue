<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { RefreshCw, Truck, Package, Activity } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Modal from '@/components/ui/Modal.vue'
import {
  getChannels, getShops, getAdapters, syncOrders, pushShipment, pushStock,
  type Channel, type Shop, type AdapterInfo, type SyncOutcome,
} from '@/api'

const channels = ref<Channel[]>([])
const shops = ref<Shop[]>([])
const adapters = ref<AdapterInfo[]>([])
const loading = ref(false)
const message = ref('')
const busyShop = ref<number | null>(null)

const shipmentOpen = ref(false)
const shipmentForm = ref({ orderId: 0, trackingNo: '', carrier: '顺丰速运' })
const stockOpen = ref(false)
const stockForm = ref({ shopId: 0, items: [{ channelSkuCode: '', quantity: 0 }] })

const channelMap = Object.fromEntries(channels.value.map(c => [c.channelId, c.channelName]))
function chName(id: number) { return channels.value.find(c => c.channelId === id)?.channelName ?? `#${id}` }

async function load() {
  loading.value = true
  try {
    const [c, s, a] = await Promise.all([getChannels(), getShops(), getAdapters()])
    channels.value = c
    shops.value = s
    adapters.value = a
  } catch { /* ignore */ } finally { loading.value = false }
}

async function doSync(shop: Shop) {
  busyShop.value = shop.shopId
  message.value = ''
  try {
    const r: SyncOutcome = await syncOrders(shop.shopId)
    message.value = `${shop.shopName}：拉取 ${r.fetched} 笔，新建 ${r.created}，跳过 ${r.skipped}`
  } catch (e) { message.value = (e as Error).message }
  finally { busyShop.value = null }
}

async function doShipment() {
  try {
    await pushShipment(shipmentForm.value.orderId, shipmentForm.value.trackingNo, shipmentForm.value.carrier)
    shipmentOpen.value = false
    message.value = '发货回传成功'
  } catch (e) { message.value = (e as Error).message }
}

async function doStock() {
  try {
    await pushStock(stockForm.value.shopId, stockForm.value.items.map(i => ({ channelSkuCode: i.channelSkuCode, quantity: Number(i.quantity) })))
    stockOpen.value = false
    message.value = '库存回写成功'
  } catch (e) { message.value = (e as Error).message }
}

onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="渠道与店铺" sub="电商平台渠道、店铺绑定与适配器接入状态" />
    <div class="p-5 space-y-5">
      <p v-if="message" class="text-[12px] text-primary bg-primary-soft border border-border rounded px-3 py-2">{{ message }}</p>

      <!-- 适配器诊断 -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <div class="flex items-center gap-2 px-4 py-3 border-b border-border">
          <Activity :size="15" class="text-primary" /><span class="text-[13px] font-semibold">适配器状态</span>
        </div>
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr><th class="text-left px-4 py-2 font-medium">渠道编码</th><th class="text-left px-4 py-2 font-medium">运行模式</th><th class="text-left px-4 py-2 font-medium">说明</th></tr></thead>
          <tbody>
            <tr v-for="a in adapters" :key="a.channelCode" class="border-t border-border">
              <td class="px-4 py-2 font-mono-tnum font-medium">{{ a.channelCode }}</td>
              <td class="px-4 py-2"><StatusBadge :value="a.mode" /></td>
              <td class="px-4 py-2 text-muted-foreground">{{ a.description }}</td>
            </tr>
            <tr v-if="adapters.length === 0"><td colspan="3" class="text-center py-6 text-muted-foreground">暂无适配器</td></tr>
          </tbody>
        </table>
      </div>

      <!-- 渠道 + 店铺 -->
      <div class="grid grid-cols-3 gap-4">
        <div v-for="c in channels" :key="c.channelId" class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
          <div class="flex items-center justify-between">
            <span class="text-[13px] font-semibold">{{ c.channelName }}</span>
            <StatusBadge :value="c.status" />
          </div>
          <div class="text-[11px] text-muted-foreground font-mono-tnum mt-1">{{ c.channelCode }} · {{ c.channelType }}</div>
          <div class="text-[12px] text-muted-foreground mt-3">{{ shops.filter(s => s.channelId === c.channelId).length }} 个店铺</div>
        </div>
      </div>

      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <div class="px-4 py-3 border-b border-border text-[13px] font-semibold">店铺列表</div>
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">店铺编码</th><th class="text-left px-4 py-2.5 font-medium">店铺名称</th>
            <th class="text-left px-4 py-2.5 font-medium">所属渠道</th><th class="text-left px-4 py-2.5 font-medium">区域</th>
            <th class="text-left px-4 py-2.5 font-medium">币种</th><th class="text-left px-4 py-2.5 font-medium">状态</th>
            <th class="text-right px-4 py-2.5 font-medium">操作</th>
          </tr></thead>
          <tbody>
            <tr v-if="loading"><td colspan="7" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="shops.length === 0"><td colspan="7" class="text-center py-8 text-muted-foreground">暂无店铺</td></tr>
            <tr v-for="s in shops" :key="s.shopId" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2.5 font-mono-tnum">{{ s.shopCode }}</td><td class="px-4 py-2.5 font-medium">{{ s.shopName }}</td>
              <td class="px-4 py-2.5">{{ chName(s.channelId) }}</td><td class="px-4 py-2.5 text-muted-foreground">{{ s.region || '—' }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ s.currency }}</td><td class="px-4 py-2.5"><StatusBadge :value="s.status" /></td>
              <td class="px-4 py-2.5">
                <div class="flex items-center justify-end gap-1">
                  <button class="inline-flex items-center gap-1 px-2 py-1 text-[11px] border border-border rounded bg-card cursor-pointer disabled:opacity-40" :disabled="busyShop === s.shopId" @click="doSync(s)">
                    <RefreshCw :size="12" :class="busyShop === s.shopId ? 'animate-spin' : ''" /> 拉单
                  </button>
                  <button title="库存回写" class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-muted cursor-pointer border-0 bg-transparent" @click="stockForm.shopId = s.shopId; stockOpen = true"><Package :size="14" /></button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="flex gap-3">
        <button class="inline-flex items-center gap-1 px-3 py-2 bg-card border border-border rounded text-[12px] cursor-pointer" @click="shipmentForm.orderId = 0; shipmentOpen = true"><Truck :size="13" /> 发货回传</button>
      </div>
    </div>

    <!-- 发货回传弹窗 -->
    <Modal :open="shipmentOpen" title="发货回传" @close="shipmentOpen = false">
      <div class="space-y-3">
        <label class="block text-[12px]">订单 ID<input v-model.number="shipmentForm.orderId" type="number" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">快递单号<input v-model="shipmentForm.trackingNo" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">快递公司<input v-model="shipmentForm.carrier" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="shipmentOpen = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="doShipment">回传</button>
      </template>
    </Modal>

    <!-- 库存回写弹窗 -->
    <Modal :open="stockOpen" title="库存回写" @close="stockOpen = false">
      <div class="space-y-3">
        <div v-for="(it, idx) in stockForm.items" :key="idx" class="flex gap-2 items-center">
          <input v-model="it.channelSkuCode" placeholder="渠道 SKU 编码" class="flex-1 h-8 px-2 border border-border rounded bg-card text-[12px]" />
          <input v-model.number="it.quantity" type="number" placeholder="数量" class="w-24 h-8 px-2 border border-border rounded bg-card text-[12px]" />
          <button class="text-danger border-0 bg-transparent cursor-pointer" @click="stockForm.items.splice(idx, 1)">×</button>
        </div>
        <button class="text-[12px] text-primary border-0 bg-transparent cursor-pointer" @click="stockForm.items.push({ channelSkuCode: '', quantity: 0 })">+ 添加 SKU</button>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="stockOpen = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="doStock">回写</button>
      </template>
    </Modal>
  </div>
</template>
