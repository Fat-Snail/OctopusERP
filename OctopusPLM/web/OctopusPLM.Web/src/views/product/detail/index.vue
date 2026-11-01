<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  ArrowLeft, Pencil, SendHorizontal, CheckCircle2, XCircle,
  Upload, ArrowDownToLine, Trash2, ImageOff, Package, Tag,
  User, Clock, Layers, FileText,
} from '@lucide/vue'
import BadgeVue from '@/components/ui/badge.vue'
import ButtonVue from '@/components/ui/button.vue'
import {
  getProductById, submitProduct, approveProduct,
  rejectProduct, publishProduct, discontinueProduct, deleteProduct,
} from '@/api/product'
import type { ProductResponse, ReviewActionRequest } from '@/api/product/types'

const route = useRoute()
const router = useRouter()
const productId = Number(route.params['id'])

const product = ref<ProductResponse | null>(null)
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    product.value = await getProductById(productId)
  } catch (e: unknown) {
    error.value = (e as Error).message || '加载失败'
  } finally {
    loading.value = false
  }
}

onMounted(load)

// ── Status ────────────────────────────────────────────────────────────────────
const statusVariant: Record<string, 'info' | 'warning' | 'success' | 'danger' | 'default'> = {
  draft: 'info',
  pending_review: 'warning',
  approved: 'success',
  rejected: 'danger',
  active: 'success',
  discontinued: 'info',
}

const versionStatusLabel: Record<string, string> = {
  draft: '草稿',
  active: '已发布',
  archived: '已归档',
}

const reviewActionLabel: Record<string, string> = {
  submit: '提交审核',
  approve: '审核通过',
  reject: '审核驳回',
  publish: '上架',
  discontinue: '下架',
  cancel: '撤回',
}

const reviewActionVariant: Record<string, 'info' | 'success' | 'danger' | 'warning' | 'default'> = {
  submit: 'info',
  approve: 'success',
  reject: 'danger',
  publish: 'success',
  discontinue: 'warning',
  cancel: 'default',
}

// ── Review dialog ─────────────────────────────────────────────────────────────
const reviewDialog = ref<{
  visible: boolean
  type: 'approve' | 'reject' | 'discontinue' | null
  comment: string
  loading: boolean
}>({ visible: false, type: null, comment: '', loading: false })

function openReviewDialog(type: 'approve' | 'reject' | 'discontinue') {
  reviewDialog.value = { visible: true, type, comment: '', loading: false }
}

const reviewDialogTitle = computed(() => {
  const t = reviewDialog.value.type
  if (t === 'approve') return '审核通过'
  if (t === 'reject') return '驳回审核'
  if (t === 'discontinue') return '下架商品'
  return ''
})

async function submitReview() {
  const d = reviewDialog.value
  d.loading = true
  const req: ReviewActionRequest = { comment: d.comment || undefined }
  try {
    if (d.type === 'approve') await approveProduct(productId, req)
    else if (d.type === 'reject') await rejectProduct(productId, req)
    else if (d.type === 'discontinue') await discontinueProduct(productId, req)
    d.visible = false
    await load()
  } catch (e: unknown) {
    console.error('操作失败:', (e as Error).message)
  } finally {
    d.loading = false
  }
}

async function handleSubmit() {
  if (!confirm('确定提交审核？')) return
  try { await submitProduct(productId); await load() }
  catch (e: unknown) { console.error((e as Error).message) }
}

async function handlePublish() {
  if (!confirm('确定上架该商品？')) return
  try { await publishProduct(productId); await load() }
  catch (e: unknown) { console.error((e as Error).message) }
}

async function handleDelete() {
  if (!confirm('确定删除该商品？此操作不可恢复。')) return
  try { await deleteProduct(productId); router.push('/plm/product') }
  catch (e: unknown) { console.error((e as Error).message) }
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function formatDate(val: string | null | undefined): string {
  if (!val) return '-'
  const d = new Date(val)
  if (Number.isNaN(d.getTime())) return val
  return d.toLocaleString('zh-CN', { timeZone: 'Asia/Shanghai', hour12: false })
}

function formatDateShort(val: string | null | undefined): string {
  if (!val) return '-'
  const d = new Date(val)
  if (Number.isNaN(d.getTime())) return val
  return d.toLocaleDateString('zh-CN', { timeZone: 'Asia/Shanghai' })
}

const attrEntries = computed(() =>
  product.value ? Object.entries(product.value.attributes) : []
)
</script>

<template>
  <div class="space-y-4 pb-8">
    <!-- Loading / Error -->
    <div v-if="loading" class="flex items-center justify-center h-48 text-[13px] text-muted-foreground">
      加载中...
    </div>
    <div v-else-if="error" class="flex items-center justify-center h-48 text-[13px] text-danger">
      {{ error }}
    </div>

    <template v-else-if="product">
      <!-- Header -->
      <div class="flex items-center gap-3 flex-wrap">
        <button
          class="flex items-center gap-1 text-[12px] text-muted-foreground hover:text-foreground transition-colors cursor-pointer border-0 bg-transparent p-0"
          @click="router.push('/plm/product')"
        >
          <ArrowLeft :size="14" />
          返回列表
        </button>
        <div class="w-px h-4 bg-border" />
        <h1 class="text-[16px] font-semibold text-foreground flex-1 min-w-0 truncate">{{ product.productName }}</h1>
        <BadgeVue :variant="statusVariant[product.status] ?? 'default'">{{ product.statusLabel }}</BadgeVue>

        <!-- Action buttons -->
        <div class="flex items-center gap-2">
          <ButtonVue
            v-if="product.status === 'draft' || product.status === 'rejected'"
            variant="outline" size="sm"
            @click="router.push(`/plm/product/edit/${product.productId}`)"
          >
            <Pencil :size="13" />
            编辑
          </ButtonVue>
          <ButtonVue
            v-if="product.status === 'draft' || product.status === 'rejected'"
            variant="outline" size="sm"
            @click="handleSubmit"
          >
            <SendHorizontal :size="13" />
            提交审核
          </ButtonVue>
          <ButtonVue
            v-if="product.status === 'pending_review'"
            size="sm"
            @click="openReviewDialog('approve')"
          >
            <CheckCircle2 :size="13" />
            通过
          </ButtonVue>
          <ButtonVue
            v-if="product.status === 'pending_review'"
            variant="outline" size="sm"
            @click="openReviewDialog('reject')"
          >
            <XCircle :size="13" />
            驳回
          </ButtonVue>
          <ButtonVue
            v-if="product.status === 'approved'"
            size="sm"
            @click="handlePublish"
          >
            <Upload :size="13" />
            上架
          </ButtonVue>
          <ButtonVue
            v-if="product.status === 'active'"
            variant="outline" size="sm"
            @click="openReviewDialog('discontinue')"
          >
            <ArrowDownToLine :size="13" />
            下架
          </ButtonVue>
          <ButtonVue
            v-if="product.status === 'draft'"
            variant="outline" size="sm"
            class="text-danger border-danger/40 hover:bg-danger/5"
            @click="handleDelete"
          >
            <Trash2 :size="13" />
            删除
          </ButtonVue>
        </div>
      </div>

      <!-- Main layout: left info + right image -->
      <div class="grid grid-cols-[1fr_200px] gap-4 items-start">
        <!-- Left: basic info -->
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4 space-y-3">
          <div class="flex items-center gap-2 text-[13px] font-semibold text-foreground mb-3">
            <Package :size="15" class="text-primary" />
            基本信息
          </div>
          <div class="grid grid-cols-2 gap-x-8 gap-y-2.5 text-[12px]">
            <div class="flex items-baseline gap-2">
              <span class="text-muted-foreground whitespace-nowrap w-[64px] shrink-0">所属类目</span>
              <span class="text-foreground font-medium">{{ product.categoryName || '—' }}</span>
            </div>
            <div class="flex items-baseline gap-2">
              <span class="text-muted-foreground whitespace-nowrap w-[64px] shrink-0">商品ID</span>
              <span class="font-mono-tnum text-foreground">{{ product.productId }}</span>
            </div>
            <div class="flex items-baseline gap-2">
              <User :size="12" class="text-muted-foreground shrink-0 mt-[3px]" />
              <span class="text-muted-foreground whitespace-nowrap shrink-0">创建人</span>
              <span class="text-foreground ml-1">{{ product.createdByName || '—' }}</span>
            </div>
            <div class="flex items-baseline gap-2">
              <Clock :size="12" class="text-muted-foreground shrink-0 mt-[3px]" />
              <span class="text-muted-foreground whitespace-nowrap shrink-0">创建时间</span>
              <span class="text-foreground ml-1">{{ formatDate(product.createdAt) }}</span>
            </div>
            <div v-if="product.reviews.length" class="flex items-baseline gap-2">
              <User :size="12" class="text-muted-foreground shrink-0 mt-[3px]" />
              <span class="text-muted-foreground whitespace-nowrap shrink-0">最后审核人</span>
              <span class="text-foreground ml-1">{{ product.reviews[product.reviews.length - 1].reviewerName || '—' }}</span>
            </div>
            <div class="flex items-baseline gap-2">
              <Clock :size="12" class="text-muted-foreground shrink-0 mt-[3px]" />
              <span class="text-muted-foreground whitespace-nowrap shrink-0">最后更新</span>
              <span class="text-foreground ml-1">{{ formatDate(product.updatedAt) }}</span>
            </div>
          </div>

          <!-- Description -->
          <div v-if="product.description" class="pt-2 border-t border-border">
            <p class="text-[12px] text-muted-foreground mb-1">商品描述</p>
            <p class="text-[12px] text-foreground leading-relaxed whitespace-pre-line">{{ product.description }}</p>
          </div>
        </div>

        <!-- Right: main image -->
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-3 flex flex-col items-center gap-2">
          <div class="w-full aspect-square rounded-[var(--radius)] overflow-hidden bg-muted flex items-center justify-center">
            <img
              v-if="product.mainImage"
              :src="product.mainImage"
              alt="主图"
              class="w-full h-full object-cover"
              @error="($event.target as HTMLImageElement).style.display = 'none'"
            />
            <ImageOff v-else :size="28" class="text-muted-foreground" />
          </div>
          <p class="text-[10px] text-muted-foreground">主图</p>
        </div>
      </div>

      <!-- Category model version -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
        <div class="flex items-center gap-2 text-[13px] font-semibold text-foreground mb-3">
          <Layers :size="15" class="text-primary" />
          类目模版版本
        </div>
        <div v-if="product.modelVersionInfo" class="grid grid-cols-2 gap-x-8 gap-y-2.5 text-[12px]">
          <div class="flex items-baseline gap-2">
            <span class="text-muted-foreground whitespace-nowrap w-[64px] shrink-0">版本号</span>
            <span class="font-mono-tnum font-semibold text-foreground">{{ product.modelVersionInfo.versionNo }}</span>
          </div>
          <div class="flex items-baseline gap-2">
            <span class="text-muted-foreground whitespace-nowrap w-[64px] shrink-0">版本状态</span>
            <span
              class="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium"
              :class="product.modelVersionInfo.status === 'active'
                ? 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
                : 'bg-muted text-muted-foreground'"
            >
              {{ versionStatusLabel[product.modelVersionInfo.status] ?? product.modelVersionInfo.status }}
            </span>
          </div>
          <div class="flex items-baseline gap-2">
            <span class="text-muted-foreground whitespace-nowrap w-[64px] shrink-0">模版创建人</span>
            <span class="text-foreground">{{ product.modelVersionInfo.createdByName || '—' }}</span>
          </div>
          <div class="flex items-baseline gap-2">
            <span class="text-muted-foreground whitespace-nowrap w-[64px] shrink-0">发布人</span>
            <span class="text-foreground">{{ product.modelVersionInfo.publishedByName || '—' }}</span>
          </div>
          <div class="flex items-baseline gap-2">
            <span class="text-muted-foreground whitespace-nowrap w-[64px] shrink-0">生效时间</span>
            <span class="text-foreground">{{ formatDateShort(product.modelVersionInfo.effectiveFrom) }}</span>
          </div>
          <div class="flex items-baseline gap-2">
            <span class="text-muted-foreground whitespace-nowrap w-[64px] shrink-0">发布时间</span>
            <span class="text-foreground">{{ formatDate(product.modelVersionInfo.publishedAt) }}</span>
          </div>
          <div v-if="product.modelVersionInfo.changeSummary" class="col-span-2 flex items-baseline gap-2">
            <span class="text-muted-foreground whitespace-nowrap w-[64px] shrink-0">变更说明</span>
            <span class="text-foreground">{{ product.modelVersionInfo.changeSummary }}</span>
          </div>
        </div>
        <div v-else class="text-[12px] text-muted-foreground italic">该类目暂无已发布的模版版本</div>
      </div>

      <!-- Attributes -->
      <div v-if="attrEntries.length" class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
        <div class="flex items-center gap-2 text-[13px] font-semibold text-foreground mb-3">
          <Tag :size="15" class="text-primary" />
          商品属性
        </div>
        <div class="grid grid-cols-3 gap-x-8 gap-y-2 text-[12px]">
          <div v-for="[k, v] in attrEntries" :key="k" class="flex items-baseline gap-2 min-w-0">
            <span class="text-muted-foreground whitespace-nowrap shrink-0 max-w-[100px] truncate" :title="k">{{ k }}</span>
            <span class="text-foreground truncate" :title="v">{{ v || '—' }}</span>
          </div>
        </div>
      </div>

      <!-- SKU table -->
      <div v-if="product.skus.length" class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <div class="flex items-center gap-2 text-[13px] font-semibold text-foreground p-4 border-b border-border">
          <FileText :size="15" class="text-primary" />
          SKU 列表
          <span class="text-[11px] text-muted-foreground font-normal ml-1">共 {{ product.skus.length }} 个</span>
        </div>
        <table class="w-full text-[12px]">
          <thead>
            <tr class="border-b border-border bg-subtle">
              <th class="text-left px-3 py-2 text-[11px] uppercase tracking-[0.04em] text-muted-foreground font-medium">SKU编码</th>
              <th class="text-left px-3 py-2 text-[11px] uppercase tracking-[0.04em] text-muted-foreground font-medium">规格</th>
              <th class="text-right px-3 py-2 text-[11px] uppercase tracking-[0.04em] text-muted-foreground font-medium">售价</th>
              <th class="text-right px-3 py-2 text-[11px] uppercase tracking-[0.04em] text-muted-foreground font-medium">成本</th>
              <th class="text-right px-3 py-2 text-[11px] uppercase tracking-[0.04em] text-muted-foreground font-medium">库存</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="sku in product.skus"
              :key="sku.skuId"
              class="border-b border-border last:border-0"
            >
              <td class="px-3 py-2 font-mono-tnum text-[11px] text-muted-foreground">{{ sku.skuCode }}</td>
              <td class="px-3 py-2 text-muted-foreground">
                <span v-if="Object.keys(sku.saleAttributes).length">
                  {{ Object.entries(sku.saleAttributes).map(([k, v]) => `${k}: ${v}`).join(' / ') }}
                </span>
                <span v-else class="text-muted-foreground/50">—</span>
              </td>
              <td class="px-3 py-2 font-mono-tnum text-right">¥{{ sku.price.toFixed(2) }}</td>
              <td class="px-3 py-2 font-mono-tnum text-right text-muted-foreground">
                {{ sku.costPrice != null ? `¥${sku.costPrice.toFixed(2)}` : '—' }}
              </td>
              <td class="px-3 py-2 font-mono-tnum text-right">{{ sku.stock }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Review history -->
      <div v-if="product.reviews.length" class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
        <div class="flex items-center gap-2 text-[13px] font-semibold text-foreground mb-4">
          <Clock :size="15" class="text-primary" />
          审核历史
        </div>
        <div class="relative pl-5 space-y-4">
          <!-- vertical line -->
          <div class="absolute left-[7px] top-2 bottom-2 w-px bg-border" />
          <div
            v-for="(rev, idx) in product.reviews"
            :key="rev.reviewId"
            class="relative flex gap-3"
          >
            <!-- dot -->
            <div
              class="absolute -left-5 top-[3px] w-3.5 h-3.5 rounded-full border-2 border-border bg-card flex items-center justify-center"
              :class="idx === product.reviews.length - 1 ? 'border-primary bg-primary/10' : ''"
            />
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 flex-wrap">
                <BadgeVue :variant="reviewActionVariant[rev.action] ?? 'default'" class="text-[10px]">
                  {{ reviewActionLabel[rev.action] ?? rev.action }}
                </BadgeVue>
                <span class="text-[12px] font-medium text-foreground">{{ rev.reviewerName || '系统' }}</span>
                <span class="text-[11px] text-muted-foreground">{{ formatDate(rev.createdAt) }}</span>
              </div>
              <p v-if="rev.comment" class="mt-1 text-[12px] text-muted-foreground leading-relaxed">
                {{ rev.comment }}
              </p>
            </div>
          </div>
        </div>
      </div>

      <!-- Extra images -->
      <div v-if="product.images.length > 1" class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
        <div class="flex items-center gap-2 text-[13px] font-semibold text-foreground mb-3">
          商品图集
          <span class="text-[11px] text-muted-foreground font-normal">{{ product.images.length }} 张</span>
        </div>
        <div class="flex flex-wrap gap-2">
          <div
            v-for="(img, i) in product.images"
            :key="i"
            class="w-20 h-20 rounded-[var(--radius)] border border-border overflow-hidden bg-muted flex items-center justify-center"
          >
            <img :src="img" alt="" class="w-full h-full object-cover" @error="($event.target as HTMLImageElement).style.display='none'" />
          </div>
        </div>
      </div>
    </template>

    <!-- Review dialog -->
    <Teleport to="body">
      <div
        v-if="reviewDialog.visible"
        class="fixed inset-0 z-50 flex items-center justify-center"
        @click.self="reviewDialog.visible = false"
      >
        <div class="absolute inset-0 bg-black/50" />
        <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[420px] p-6 space-y-4">
          <h3 class="text-[14px] font-semibold">{{ reviewDialogTitle }}</h3>
          <div class="space-y-1">
            <label class="text-[12px] text-muted-foreground">
              {{ reviewDialog.type === 'reject' ? '驳回原因（必填）' : '备注（可选）' }}
            </label>
            <textarea
              v-model="reviewDialog.comment"
              class="w-full h-24 rounded-[var(--radius)] border border-input bg-background px-3 py-2 text-[12px] text-foreground resize-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              :placeholder="reviewDialog.type === 'reject' ? '请填写驳回原因...' : '可填写备注说明...'"
            />
          </div>
          <div class="flex justify-end gap-2">
            <ButtonVue variant="outline" size="sm" @click="reviewDialog.visible = false">取消</ButtonVue>
            <ButtonVue
              size="sm"
              :disabled="reviewDialog.loading || (reviewDialog.type === 'reject' && !reviewDialog.comment.trim())"
              @click="submitReview"
            >
              {{ reviewDialog.loading ? '处理中...' : '确认' }}
            </ButtonVue>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
