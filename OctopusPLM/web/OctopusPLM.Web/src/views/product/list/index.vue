<script setup lang="ts">
import { ref, onMounted, h, computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  useVueTable,
  getCoreRowModel,
  createColumnHelper,
  FlexRender,
} from '@tanstack/vue-table'
import {
  Plus, Pencil, Trash2, SendHorizontal, ScanSearch,
  CheckCircle2, XCircle, Upload, ArrowDownToLine, FileText, Loader2,
} from 'lucide-vue-next'
import ButtonVue from '@/components/ui/button.vue'
import InputVue from '@/components/ui/input.vue'
import BadgeVue from '@/components/ui/badge.vue'
import {
  getProductList, deleteProduct, submitProduct,
  approveProduct, rejectProduct, publishProduct, discontinueProduct,
  getProductStats, importFrom1688,
} from '@/api/product'
import type { ProductListRow, ProductStatsResponse, Import1688Result } from '@/api/product/types'

const router = useRouter()
const loading = ref(false)
const list = ref<ProductListRow[]>([])
const page = ref(1)
const size = ref(20)
const total = ref(0)
const keyword = ref('')
const statusFilter = ref('')
const stats = ref<ProductStatsResponse | null>(null)

// ── Review dialog ─────────────────────────────────────────────────────────────
const reviewDialog = ref<{
  visible: boolean
  type: 'approve' | 'reject' | 'discontinue' | null
  productId: number
  comment: string
  loading: boolean
}>({ visible: false, type: null, productId: 0, comment: '', loading: false })

function openReviewDialog(type: 'approve' | 'reject' | 'discontinue', id: number) {
  reviewDialog.value = { visible: true, type, productId: id, comment: '', loading: false }
}

async function submitReview() {
  const d = reviewDialog.value
  d.loading = true
  try {
    if (d.type === 'approve') await approveProduct(d.productId, { comment: d.comment || undefined })
    else if (d.type === 'reject') await rejectProduct(d.productId, { comment: d.comment })
    else if (d.type === 'discontinue') await discontinueProduct(d.productId, { comment: d.comment || undefined })
    d.visible = false
    await Promise.all([loadData(), loadStats()])
  } catch (e: unknown) {
    console.error('操作失败:', (e as Error).message)
  } finally {
    d.loading = false
  }
}

// ── Status config ─────────────────────────────────────────────────────────────
const statusVariant: Record<string, 'default' | 'info' | 'warning' | 'success' | 'danger'> = {
  draft: 'info',
  pending_review: 'warning',
  approved: 'success',
  rejected: 'danger',
  active: 'success',
  discontinued: 'info',
}

const statusOptions = [
  { value: '', label: '全部状态' },
  { value: 'draft', label: '草稿' },
  { value: 'pending_review', label: '待审核' },
  { value: 'approved', label: '已通过' },
  { value: 'rejected', label: '已驳回' },
  { value: 'active', label: '已上架' },
  { value: 'discontinued', label: '已下架' },
]

// ── Columns ───────────────────────────────────────────────────────────────────
const columnHelper = createColumnHelper<ProductListRow>()

const columns = [
  columnHelper.accessor('productName', {
    header: '商品名称',
    cell: info => h('span', { class: 'text-[12px] font-medium text-foreground' }, info.getValue()),
    size: 220,
  }),
  columnHelper.accessor('categoryName', {
    header: '类目',
    cell: info => h('span', { class: 'text-[12px] text-muted-foreground' }, info.getValue()),
    size: 120,
  }),
  columnHelper.display({
    id: 'price',
    header: '价格区间',
    cell: ({ row }) => {
      const r = row.original
      if (r.minPrice > 0) {
        return h('span', { class: 'font-mono-tnum text-[12px]' }, `¥${r.minPrice} ~ ¥${r.maxPrice}`)
      }
      return h('span', { class: 'text-[12px] text-muted-foreground' }, '未设置')
    },
    size: 160,
  }),
  columnHelper.accessor('totalStock', {
    header: '总库存',
    cell: info => h('span', { class: 'font-mono-tnum text-[12px] text-right block' }, String(info.getValue())),
    size: 80,
  }),
  columnHelper.accessor('status', {
    header: '状态',
    cell: ({ row }) => {
      const r = row.original
      const variant = statusVariant[r.status] ?? 'info'
      return h(BadgeVue, { variant }, () => r.statusLabel)
    },
    size: 90,
  }),
  columnHelper.accessor('updatedAt', {
    header: '更新时间',
    cell: info => h('span', { class: 'text-[11px] text-muted-foreground' }, formatDate(info.getValue())),
    size: 160,
  }),
  columnHelper.display({
    id: 'actions',
    header: '操作',
    cell: ({ row }) => {
      const r = row.original
      const btns = []

      // Edit (draft / rejected)
      if (r.status === 'draft' || r.status === 'rejected') {
        btns.push(h('button', {
          class: 'text-primary text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5',
          onClick: () => router.push(`/plm/product/edit/${r.productId}`),
        }, [h(Pencil, { size: 12, class: 'inline mr-0.5' }), '编辑']))
      }

      // Submit for review
      if (r.status === 'draft' || r.status === 'rejected') {
        btns.push(h('button', {
          class: 'text-[color:var(--info)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5',
          onClick: () => handleSubmit(r.productId),
        }, [h(SendHorizontal, { size: 12, class: 'inline mr-0.5' }), '提交']))
      }

      // Approve / Reject
      if (r.status === 'pending_review') {
        btns.push(h('button', {
          class: 'text-[color:var(--success)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5',
          onClick: () => openReviewDialog('approve', r.productId),
        }, [h(CheckCircle2, { size: 12, class: 'inline mr-0.5' }), '通过']))
        btns.push(h('button', {
          class: 'text-[color:var(--danger)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5',
          onClick: () => openReviewDialog('reject', r.productId),
        }, [h(XCircle, { size: 12, class: 'inline mr-0.5' }), '驳回']))
      }

      // Publish (approved → active)
      if (r.status === 'approved') {
        btns.push(h('button', {
          class: 'text-[color:var(--success)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5',
          onClick: () => handlePublish(r.productId),
        }, [h(Upload, { size: 12, class: 'inline mr-0.5' }), '上架']))
      }

      // Discontinue (active → discontinued)
      if (r.status === 'active') {
        btns.push(h('button', {
          class: 'text-[color:var(--warning)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5',
          onClick: () => openReviewDialog('discontinue', r.productId),
        }, [h(ArrowDownToLine, { size: 12, class: 'inline mr-0.5' }), '下架']))
      }

      // Delete (draft only)
      if (r.status === 'draft') {
        btns.push(h('button', {
          class: 'text-[color:var(--danger)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0',
          onClick: () => handleDelete(r.productId),
        }, [h(Trash2, { size: 12, class: 'inline mr-0.5' }), '删除']))
      }

      return h('div', { class: 'flex items-center flex-wrap gap-0.5' }, btns)
    },
    size: 220,
  }),
]

const table = useVueTable({
  get data() { return list.value },
  columns,
  getCoreRowModel: getCoreRowModel(),
})

// ── Helpers ───────────────────────────────────────────────────────────────────
function formatDate(val: string) {
  if (!val) return '-'
  const d = new Date(val)
  if (Number.isNaN(d.getTime())) return val
  return d.toLocaleString('zh-CN', { timeZone: 'Asia/Shanghai', hour12: false })
}

async function loadStats() {
  try { stats.value = await getProductStats() } catch { /* ignore */ }
}

async function loadData() {
  loading.value = true
  try {
    const res = await getProductList({
      page: page.value,
      size: size.value,
      keyword: keyword.value || undefined,
      status: statusFilter.value || undefined,
    })
    list.value = res.rows
    total.value = res.total
  } catch (e: unknown) {
    console.error('加载商品列表失败:', (e as Error).message)
  } finally {
    loading.value = false
  }
}

function search() { page.value = 1; loadData() }
function reset() { keyword.value = ''; statusFilter.value = ''; page.value = 1; loadData() }

async function handleSubmit(id: number) {
  try { await submitProduct(id); loadData(); loadStats() }
  catch (e: unknown) { console.error('提交失败:', (e as Error).message) }
}

async function handlePublish(id: number) {
  if (!confirm('确定上架该商品？')) return
  try { await publishProduct(id); await Promise.all([loadData(), loadStats()]) }
  catch (e: unknown) { console.error('上架失败:', (e as Error).message) }
}

async function handleDelete(id: number) {
  if (!confirm('确定删除该商品？')) return
  try { await deleteProduct(id); await Promise.all([loadData(), loadStats()]) }
  catch (e: unknown) { console.error('删除失败:', (e as Error).message) }
}

const totalPages = computed(() => Math.ceil(total.value / size.value))

const statsCards = computed(() => {
  const s = stats.value
  if (!s) return []
  return [
    { label: '全部', value: s.total, filter: '' },
    { label: '草稿', value: s.draft, filter: 'draft' },
    { label: '待审核', value: s.pendingReview, filter: 'pending_review' },
    { label: '已通过', value: s.approved, filter: 'approved' },
    { label: '已驳回', value: s.rejected, filter: 'rejected' },
    { label: '已上架', value: s.active, filter: 'active' },
    { label: '已下架', value: s.discontinued, filter: 'discontinued' },
  ]
})

const reviewDialogTitle = computed(() => {
  const t = reviewDialog.value.type
  if (t === 'approve') return '审核通过'
  if (t === 'reject') return '驳回审核'
  if (t === 'discontinue') return '下架商品'
  return ''
})

// ── 1688 import dialog ────────────────────────────────────────────────────────
const importDialog = ref(false)
const importInput = ref('')
const importParsed = ref<string[]>([])
const importParseError = ref('')
const importLoading = ref(false)
const importResult = ref<Import1688Result | null>(null)

function parseImportInput() {
  importParseError.value = ''
  importParsed.value = []
  const raw = importInput.value.trim()
  if (!raw) { importParseError.value = '请粘贴 JSON 内容'; return }
  try {
    const parsed = JSON.parse(raw)
    if (Array.isArray(parsed)) {
      importParsed.value = parsed.map(item => JSON.stringify(item))
    } else {
      importParsed.value = [raw]
    }
  } catch {
    // Try JSONL (one JSON per line)
    const lines = raw.split('\n').map(l => l.trim()).filter(Boolean)
    const valid: string[] = []
    for (const line of lines) {
      try { JSON.parse(line); valid.push(line) } catch { /* skip invalid lines */ }
    }
    if (valid.length === 0) { importParseError.value = 'JSON 格式有误，无法解析'; return }
    importParsed.value = valid
  }
}

function onImportFileChange(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = () => { importInput.value = reader.result as string; parseImportInput() }
  reader.readAsText(file)
}

async function doImport() {
  parseImportInput()
  if (importParsed.value.length === 0) return
  importLoading.value = true
  importResult.value = null
  try {
    importResult.value = await importFrom1688(importParsed.value)
    await Promise.all([loadData(), loadStats()])
  } catch (e: unknown) {
    importParseError.value = (e as Error).message
  } finally {
    importLoading.value = false
  }
}

function openImportDialog() {
  importDialog.value = true
  importInput.value = ''
  importParsed.value = []
  importParseError.value = ''
  importLoading.value = false
  importResult.value = null
}

onMounted(() => { loadData(); loadStats() })
</script>

<template>
  <div class="space-y-4">
    <!-- Stats bar -->
    <div v-if="stats" class="flex items-center gap-2 flex-wrap">
      <button
        v-for="card in statsCards"
        :key="card.filter"
        class="flex items-center gap-1.5 px-3 py-1.5 rounded-[var(--radius)] border text-[12px] transition-colors"
        :class="statusFilter === card.filter
          ? 'bg-primary text-primary-foreground border-primary'
          : 'bg-card border-border text-muted-foreground hover:border-primary hover:text-foreground'"
        @click="statusFilter = card.filter; search()"
      >
        <span class="font-mono-tnum font-semibold text-[13px]">{{ card.value }}</span>
        <span>{{ card.label }}</span>
      </button>
    </div>

    <!-- Toolbar -->
    <div class="flex items-center gap-2 flex-wrap">
      <InputVue
        v-model="keyword"
        placeholder="搜索商品名称..."
        class="w-[220px]"
        @keyup.enter="search"
      />
      <select
        v-model="statusFilter"
        class="h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        @change="search"
      >
        <option v-for="opt in statusOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
      </select>
      <ButtonVue variant="outline" size="sm" @click="search">搜索</ButtonVue>
      <ButtonVue variant="ghost" size="sm" @click="reset">重置</ButtonVue>
      <div class="flex-1" />
      <ButtonVue variant="outline" size="sm" @click="router.push('/plm/product/image-search')">
        <ScanSearch :size="13" />
        以图搜商品
      </ButtonVue>
      <ButtonVue variant="outline" size="sm" @click="openImportDialog">
        <FileText :size="13" />
        1688导入
      </ButtonVue>
      <ButtonVue size="sm" @click="router.push('/plm/product/create')">
        <Plus :size="13" />
        新建物料
      </ButtonVue>
      <ButtonVue variant="outline" size="sm" @click="router.push('/plm/category/manage')">
        类目管理
      </ButtonVue>
      <ButtonVue variant="outline" size="sm" @click="router.push('/plm/category-model')">
        类目模型
      </ButtonVue>
    </div>

    <!-- Table -->
    <div class="border border-border rounded-[var(--radius-lg)] overflow-hidden bg-card">
      <div v-if="loading" class="h-40 flex items-center justify-center text-[12px] text-muted-foreground">
        加载中...
      </div>
      <table v-else class="w-full text-[12px]">
        <thead>
          <tr v-for="headerGroup in table.getHeaderGroups()" :key="headerGroup.id">
            <th
              v-for="header in headerGroup.headers"
              :key="header.id"
              class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border"
              :style="{ width: header.getSize() + 'px' }"
            >
              <FlexRender
                v-if="!header.isPlaceholder"
                :render="header.column.columnDef.header"
                :props="header.getContext()"
              />
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="table.getRowModel().rows.length === 0">
            <td :colspan="columns.length" class="text-center py-10 text-muted-foreground text-[12px]">
              暂无数据
            </td>
          </tr>
          <tr
            v-for="row in table.getRowModel().rows"
            :key="row.id"
            class="border-b border-border last:border-0 hover:bg-subtle transition-colors"
          >
            <td
              v-for="cell in row.getVisibleCells()"
              :key="cell.id"
              class="px-3 py-[7px] h-[var(--row-h)]"
            >
              <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Pagination -->
    <div class="flex items-center justify-between text-[12px] text-muted-foreground">
      <span>共 {{ total }} 条</span>
      <div class="flex items-center gap-1">
        <ButtonVue
          variant="outline"
          size="sm"
          :disabled="page <= 1"
          @click="page > 1 && (page--, loadData())"
        >
          上一页
        </ButtonVue>
        <span class="px-2 tabular-nums">{{ page }} / {{ totalPages || 1 }}</span>
        <ButtonVue
          variant="outline"
          size="sm"
          :disabled="page >= totalPages"
          @click="page < totalPages && (page++, loadData())"
        >
          下一页
        </ButtonVue>
      </div>
    </div>

    <!-- 1688 Import dialog -->
    <Teleport to="body">
      <div
        v-if="importDialog"
        class="fixed inset-0 z-50 flex items-center justify-center"
        @click.self="importDialog = false"
      >
        <div class="absolute inset-0 bg-black/50" />
        <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[600px] max-h-[80vh] flex flex-col">
          <!-- Header -->
          <div class="px-6 py-4 border-b border-border flex items-center justify-between shrink-0">
            <h3 class="text-[14px] font-semibold flex items-center gap-2">
              <FileText :size="16" class="text-primary" />
              从1688导入商品
            </h3>
            <button
              class="text-muted-foreground hover:text-foreground transition-colors cursor-pointer border-0 bg-transparent p-0"
              @click="importDialog = false"
            >✕</button>
          </div>

          <!-- Body -->
          <div class="flex-1 overflow-y-auto px-6 py-4 space-y-4">
            <div v-if="!importResult" class="space-y-4">
              <p class="text-[12px] text-muted-foreground leading-relaxed">
                粘贴1688商品 API 返回的 JSON，支持单个对象、对象数组或每行一条（JSONL）。
                已导入过的商品（相同 productID）会自动跳过。
              </p>

              <!-- Textarea -->
              <textarea
                v-model="importInput"
                class="w-full h-[200px] rounded-[var(--radius)] border border-input bg-background px-3 py-2 text-[11px] text-foreground font-mono resize-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                placeholder='粘贴 JSON，例：{"success":true,"productInfo":{"productID":1234,...}}'
                @input="importParsed = []; importParseError = ''; importResult = null"
              />

              <!-- File upload -->
              <div class="flex items-center gap-3">
                <label class="inline-flex items-center gap-1.5 cursor-pointer px-3 h-[var(--row-h)] rounded-[var(--radius)] border border-input text-[12px] text-muted-foreground hover:text-foreground hover:border-primary transition-colors">
                  <Upload :size="12" />
                  选择 JSON 文件
                  <input type="file" accept=".json,.jsonl,.txt" class="hidden" @change="onImportFileChange" />
                </label>
                <span class="text-[11px] text-muted-foreground">支持 .json / .jsonl / .txt</span>
              </div>

              <!-- Parse preview -->
              <div v-if="importParsed.length > 0" class="flex items-center gap-2 text-[12px] text-success">
                <CheckCircle2 :size="13" />
                已解析 {{ importParsed.length }} 条商品，点击「开始导入」继续
              </div>
              <p v-if="importParseError" class="text-[12px] text-danger">{{ importParseError }}</p>
            </div>

            <!-- Import result -->
            <div v-else class="space-y-4">
              <!-- Summary -->
              <div class="grid grid-cols-4 gap-3">
                <div class="rounded-[var(--radius)] border border-border bg-subtle p-3 text-center">
                  <div class="text-[18px] font-mono-tnum font-semibold">{{ importResult.total }}</div>
                  <div class="text-[11px] text-muted-foreground mt-0.5">合计</div>
                </div>
                <div class="rounded-[var(--radius)] border border-border bg-subtle p-3 text-center">
                  <div class="text-[18px] font-mono-tnum font-semibold text-[var(--success)]">{{ importResult.imported }}</div>
                  <div class="text-[11px] text-muted-foreground mt-0.5">导入成功</div>
                </div>
                <div class="rounded-[var(--radius)] border border-border bg-subtle p-3 text-center">
                  <div class="text-[18px] font-mono-tnum font-semibold text-muted-foreground">{{ importResult.skipped }}</div>
                  <div class="text-[11px] text-muted-foreground mt-0.5">已跳过</div>
                </div>
                <div class="rounded-[var(--radius)] border border-border bg-subtle p-3 text-center">
                  <div class="text-[18px] font-mono-tnum font-semibold text-[var(--danger)]">{{ importResult.failed }}</div>
                  <div class="text-[11px] text-muted-foreground mt-0.5">失败</div>
                </div>
              </div>

              <!-- Item table -->
              <div class="border border-border rounded-[var(--radius)] overflow-hidden">
                <table class="w-full text-[11px]">
                  <thead>
                    <tr class="border-b border-border bg-subtle">
                      <th class="text-left px-3 py-2 text-muted-foreground font-medium">商品名称</th>
                      <th class="text-left px-3 py-2 text-muted-foreground font-medium w-[80px]">状态</th>
                      <th class="text-left px-3 py-2 text-muted-foreground font-medium">备注</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="item in importResult.items"
                      :key="item.sourceId"
                      class="border-b border-border last:border-0"
                    >
                      <td class="px-3 py-2 max-w-[240px] truncate" :title="item.subject">{{ item.subject || `ID:${item.sourceId}` }}</td>
                      <td class="px-3 py-2">
                        <BadgeVue
                          :variant="item.status === 'imported' ? 'success' : item.status === 'skipped' ? 'default' : 'danger'"
                          class="text-[10px]"
                        >
                          {{ item.status === 'imported' ? '已导入' : item.status === 'skipped' ? '已跳过' : '失败' }}
                        </BadgeVue>
                      </td>
                      <td class="px-3 py-2 text-muted-foreground truncate max-w-[160px]" :title="item.error ?? undefined">
                        {{ item.status === 'imported' ? `商品ID: ${item.productId}` : (item.error || '-') }}
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <!-- Footer -->
          <div class="px-6 py-4 border-t border-border shrink-0 flex justify-between items-center">
            <button
              v-if="importResult"
              class="text-[12px] text-muted-foreground hover:text-foreground cursor-pointer border-0 bg-transparent p-0 underline"
              @click="importResult = null; importParsed = []; importInput = ''"
            >重新导入</button>
            <div v-else class="text-[11px] text-muted-foreground">
              {{ importParsed.length > 0 ? `${importParsed.length} 条待导入` : '请粘贴 JSON 后解析' }}
            </div>
            <div class="flex gap-2">
              <ButtonVue variant="outline" size="sm" @click="importDialog = false">
                {{ importResult ? '关闭' : '取消' }}
              </ButtonVue>
              <ButtonVue
                v-if="!importResult"
                size="sm"
                :disabled="importLoading"
                @click="doImport"
              >
                <Loader2 v-if="importLoading" :size="13" class="animate-spin" />
                {{ importLoading ? '导入中...' : '开始导入' }}
              </ButtonVue>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Review dialog (Approve / Reject / Discontinue) -->
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
