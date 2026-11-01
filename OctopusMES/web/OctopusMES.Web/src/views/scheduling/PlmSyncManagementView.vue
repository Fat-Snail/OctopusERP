<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import PageHeader from '@/components/app/PageHeader.vue'
import { getReconciliation, getSyncRecordDetail, getSyncRecords, initializeReconciliation, retrySyncRecord } from '@/api/plmSync'
import { statusMeta, type ReconcileSummary, type SyncRecordDetail, type SyncRecordPage, type SyncStatus } from './plmSyncTypes'

const page = ref<SyncRecordPage>({ rows: [], total: 0, page: 1, pageSize: 20, hasMore: false })
const summary = ref<ReconcileSummary | null>(null)
const keyword = ref('')
const status = ref('')
const loading = ref(false)
const message = ref('')
const detail = ref<SyncRecordDetail | null>(null)
let searchTimer: ReturnType<typeof setTimeout> | undefined

const failedTotal = computed(() => page.value.rows.filter(x => x.status === 'retry_waiting' || x.status === 'manual_action').length)
const manualTotal = computed(() => page.value.rows.filter(x => x.status === 'manual_action').length)
const cards = computed(() => [
  ['PLM 商品', summary.value?.plmTotal ?? 0], ['已发布', summary.value?.releasedTotal ?? 0],
  ['MES 台账', summary.value?.localRecordTotal ?? page.value.total], ['一致', summary.value?.sameTotal ?? 0],
  ['不一致', summary.value?.differentTotal ?? 0], ['已同步', summary.value?.syncedTotal ?? 0],
  ['当前页异常', failedTotal.value], ['待人工处理', manualTotal.value],
])

async function loadRecords(targetPage = 1) {
  loading.value = true
  try {
    const result = await getSyncRecords({ status: status.value || undefined, keyword: keyword.value.trim() || undefined, page: targetPage, pageSize: 20 })
    if (result.code !== 200) throw new Error(result.msg)
    page.value = result.data
  } catch (error) { message.value = error instanceof Error ? error.message : '同步台账读取失败' }
  finally { loading.value = false }
}
async function reconcile(initialize = false) {
  loading.value = true
  try {
    const result = initialize ? await initializeReconciliation() : await getReconciliation()
    if (result.code !== 200) throw new Error(result.msg)
    summary.value = result.data
    message.value = initialize ? '初始化完成，已建立已发布制造版本台账。' : result.msg
    await loadRecords(1)
  } catch (error) { message.value = error instanceof Error ? error.message : 'PLM 对账失败' }
  finally { loading.value = false }
}
async function openDetail(id: number) {
  const result = await getSyncRecordDetail(id)
  if (result.code === 200) detail.value = result.data
  else message.value = result.msg
}
async function retry(id: number) {
  loading.value = true
  try { const result = await retrySyncRecord(id); message.value = result.msg; await loadRecords(page.value.page); await openDetail(id) }
  catch (error) { message.value = error instanceof Error ? error.message : '立即重试失败' }
  finally { loading.value = false }
}
function issueText(json?: string) {
  if (!json) return ''
  try { return (JSON.parse(json) as Array<{ path: string; message: string }>).map(x => `${x.path}：${x.message}`).join('；') }
  catch { return json }
}
watch(keyword, () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => loadRecords(1), 300) })
watch(status, () => loadRecords(1))
onMounted(() => Promise.all([reconcile(false), loadRecords(1)]))
</script>

<template>
  <div class="p-6 space-y-5">
    <PageHeader title="PLM 同步管理" description="真实 PLM 制造版本 · 自动重试 · 不可变 MES 快照" />
    <div class="flex items-center justify-between">
      <div class="text-sm" :class="summary?.available === false ? 'text-red-600' : 'text-emerald-700'">{{ message || '正在读取 PLM 与 MES 数据…' }}</div>
      <div class="flex gap-2"><button class="border border-border rounded px-3 py-2 text-sm" :disabled="loading" @click="reconcile(false)">刷新对账</button><button class="bg-primary text-primary-foreground rounded px-3 py-2 text-sm disabled:opacity-40" :disabled="loading || summary?.available === false" @click="reconcile(true)">初始化同步记录</button></div>
    </div>
    <div class="grid grid-cols-8 gap-3"><div v-for="card in cards" :key="String(card[0])" class="border border-border rounded-lg bg-card p-3"><div class="text-xs text-muted-foreground">{{ card[0] }}</div><div class="text-2xl font-semibold mt-1">{{ card[1] }}</div></div></div>
    <div class="flex gap-3"><input v-model="keyword" class="border border-border rounded px-3 py-2 text-sm w-80" placeholder="服务端搜索商品编码或名称"/><select v-model="status" class="border border-border rounded px-3 py-2 text-sm"><option value="">全部状态</option><option v-for="(meta,key) in statusMeta" :key="key" :value="key">{{ meta.label }}</option></select></div>
    <div class="bg-card border border-border rounded-lg overflow-x-auto">
      <table class="w-full text-sm"><thead class="bg-muted"><tr><th class="p-3 text-left">商品 / 制造版本</th><th class="p-3">状态</th><th class="p-3">重试</th><th class="p-3">同步时间</th><th class="p-3 text-left">错误与差异</th><th class="p-3">操作</th></tr></thead>
        <tbody><tr v-for="row in page.rows" :key="row.syncRecordId" class="border-t border-border"><td class="p-3"><div class="font-medium">{{ row.productCode || `PLM-${row.plmProductId}` }} · {{ row.productName || '等待同步商品资料' }}</div><div class="text-xs text-muted-foreground">制造版本 {{ row.plmVersion }} · 快照 {{ row.manufacturingSnapshotId || '-' }}</div></td><td class="p-3 text-center"><span :class="statusMeta[row.status as SyncStatus]?.className">{{ statusMeta[row.status as SyncStatus]?.label || row.status }}</span></td><td class="p-3 text-center">{{ row.retryCount }} 次<div v-if="row.nextRetryAt" class="text-xs text-orange-700">下次 {{ row.nextRetryAt }}</div></td><td class="p-3 text-center text-xs">{{ row.syncedAt || '-' }}</td><td class="p-3" :class="row.errorMessage ? 'text-red-600' : 'text-muted-foreground'">{{ row.errorMessage || '无错误' }}</td><td class="p-3 text-center space-x-2"><button class="border border-border rounded px-2 py-1" @click="openDetail(row.syncRecordId)">查看详情</button><button v-if="row.status !== 'synced' && row.status !== 'obsolete'" class="border border-red-300 text-red-600 rounded px-2 py-1" :disabled="loading" @click="retry(row.syncRecordId)">立即重试</button></td></tr><tr v-if="!page.rows.length"><td colspan="6" class="p-10 text-center text-muted-foreground">没有符合条件的同步记录</td></tr></tbody>
      </table>
      <div class="flex justify-between items-center p-3 border-t border-border text-sm"><span>共 {{ page.total }} 条，第 {{ page.page }} 页</span><div class="flex gap-2"><button class="border rounded px-3 py-1" :disabled="page.page <= 1 || loading" @click="loadRecords(page.page - 1)">上一页</button><button class="border rounded px-3 py-1" :disabled="!page.hasMore || loading" @click="loadRecords(page.page + 1)">下一页</button></div></div>
    </div>
    <div v-if="detail" class="fixed inset-0 z-50 bg-black/30 flex justify-end" @click.self="detail = null"><aside class="w-[620px] h-full overflow-y-auto bg-card p-6 shadow-xl space-y-5"><div class="flex justify-between"><div><h2 class="text-xl font-semibold">同步详情</h2><p class="text-sm text-muted-foreground">记录 {{ detail.record.syncRecordId }} · {{ detail.record.productCode }}</p></div><button @click="detail = null">关闭</button></div><div v-if="detail.record.errorMessage" class="border border-red-300 bg-red-50 text-red-700 rounded p-3">{{ detail.record.errorMessage }}</div><section v-if="detail.snapshot" class="border rounded p-3"><h3 class="font-medium">不可变制造快照</h3><p class="text-sm mt-2">{{ detail.snapshot.productVersion }} · BOM {{ detail.snapshot.bomVersion }} · 路线 {{ detail.snapshot.routingVersion }}</p><p class="text-xs text-muted-foreground break-all mt-1">SHA-256 {{ detail.snapshot.payloadHash }}</p><p class="text-sm mt-2">物料 {{ detail.snapshot.bomItems.length }} 项 · 工序 {{ detail.snapshot.operations.length }} 道</p></section><section><h3 class="font-medium mb-2">同步尝试</h3><div v-for="attempt in detail.attempts" :key="attempt.attemptNumber" class="border rounded p-3 mb-2 text-sm"><div class="flex justify-between"><b>第 {{ attempt.attemptNumber }} 次 · {{ attempt.status }}</b><span>{{ attempt.finishedAt }}</span></div><div v-if="attempt.errorMessage" class="text-red-600 mt-1">{{ attempt.errorCode }} · {{ attempt.errorMessage }}</div><div v-if="attempt.validationIssuesJson" class="text-red-600 text-xs mt-1">{{ issueText(attempt.validationIssuesJson) }}</div></div></section><section><h3 class="font-medium mb-2">关联生产订单</h3><div v-for="order in detail.relatedOrders" :key="order.productionOrderId" class="border rounded p-2 text-sm mb-2">{{ order.productionOrderCode }} · {{ order.status }} · {{ order.plannedQty }}</div><p v-if="!detail.relatedOrders.length" class="text-sm text-muted-foreground">暂无关联订单</p></section></aside></div>
  </div>
</template>
