<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Plus, X, Image as ImageIcon, Upload } from 'lucide-vue-next'
import ButtonVue from '@/components/ui/button.vue'
import InputVue from '@/components/ui/input.vue'
import BadgeVue from '@/components/ui/badge.vue'
import { getCategoryTree, getCategoryAttributes } from '@/api/category'
import { getProductById, createProduct, updateProduct, getProductReviews } from '@/api/product'
import { uploadImage } from '@/api/upload'
import type { CategoryTreeNode, CategoryAttributeGrouped, CategoryAttributeResponse } from '@/api/category/types'
import type { CreateSkuRequest, ReviewHistoryItem } from '@/api/product/types'

const router = useRouter()
const route = useRoute()
const isEdit = ref(route.name === 'PlmProductEdit')
const editId = ref(isEdit.value ? Number(route.params.id) : 0)

const saving = ref(false)
const errorMsg = ref('')
const categoryOptions = ref<CategoryTreeNode[]>([])
const spuAttributes = ref<CategoryAttributeResponse[]>([])
const skuDimensionGroups = ref<CategoryAttributeGrouped[]>([])
const newImageUrl = ref('')
const reviewHistory = ref<ReviewHistoryItem[]>([])

// ── Upload refs ───────────────────────────────────────────────────────────────
const mainImageInputRef = ref<HTMLInputElement | null>(null)
const galleryInputRef = ref<HTMLInputElement | null>(null)
const mainImageUploading = ref(false)
const galleryUploading = ref(false)

async function onMainImageFile(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  mainImageUploading.value = true
  try {
    const { url } = await uploadImage(file)
    form.mainImage = url
  } catch (err: unknown) {
    errorMsg.value = (err as Error).message || '上传失败'
  } finally {
    mainImageUploading.value = false
    if (mainImageInputRef.value) mainImageInputRef.value.value = ''
  }
}

async function onGalleryFile(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file || form.images.length >= 8) return
  galleryUploading.value = true
  try {
    const { url } = await uploadImage(file)
    form.images.push(url)
  } catch (err: unknown) {
    errorMsg.value = (err as Error).message || '上传失败'
  } finally {
    galleryUploading.value = false
    if (galleryInputRef.value) galleryInputRef.value.value = ''
  }
}

const selectedCategoryId = ref<number>(0)

// selected dimensions: groupName -> attrId[]
const selectedDimensions = reactive<Record<string, number[]>>({})
// selected values: attrId -> value[]
const selectedValues = reactive<Record<number, string[]>>({})

interface SkuRow {
  skuCode: string
  barcode?: string
  saleAttributes?: Record<string, string>
  price: number
  costPrice?: number
  stock: number
}

const form = reactive({
  productName: '',
  description: '',
  mainImage: '',
  attributes: {} as Record<string, string>,
  skus: [] as SkuRow[],
  images: [] as string[],
  vectorizeAfterSave: false,
})

// ── Category helpers ──────────────────────────────────────────────────────────

interface FlatCat { node: CategoryTreeNode; depth: number }

const flatCategories = computed<FlatCat[]>(() => {
  function walk(nodes: CategoryTreeNode[], depth: number): FlatCat[] {
    return nodes.flatMap(n => [{ node: n, depth }, ...walk(n.children, depth + 1)])
  }
  return walk(categoryOptions.value, 0)
})

function catIndent(depth: number) {
  return depth > 0 ? '　'.repeat(depth) + '└ ' : ''
}

function findNode(nodes: CategoryTreeNode[], id: number): CategoryTreeNode | null {
  for (const n of nodes) {
    if (n.categoryId === id) return n
    const found = findNode(n.children, id)
    if (found) return found
  }
  return null
}

function findAncestorPath(nodes: CategoryTreeNode[], id: number): number[] {
  function walk(list: CategoryTreeNode[], acc: number[]): number[] | null {
    for (const n of list) {
      const path = [...acc, n.categoryId]
      if (n.categoryId === id) return path
      if (n.children.length) {
        const found = walk(n.children, path)
        if (found) return found
      }
    }
    return null
  }
  return walk(nodes, []) ?? []
}

async function onCategoryChange(id: number) {
  selectedCategoryId.value = id
  if (!id) { spuAttributes.value = []; skuDimensionGroups.value = []; return }
  await loadAttributes(id)
}

async function loadAttributes(categoryId: number) {
  try {
    const groups = await getCategoryAttributes(categoryId)
    spuAttributes.value = groups.find(g => g.groupName === null)?.attributes ?? []
    skuDimensionGroups.value = groups.filter(g => g.groupName !== null)

    for (const key of Object.keys(selectedDimensions)) delete selectedDimensions[key]
    for (const key of Object.keys(selectedValues)) delete (selectedValues as Record<string | number, unknown>)[key]
    for (const g of skuDimensionGroups.value) {
      selectedDimensions[g.groupName!] = []
    }
  } catch (e: unknown) {
    errorMsg.value = (e as Error).message || '加载属性失败'
  }
}

// ── SKU generation ────────────────────────────────────────────────────────────

function toggleDimension(groupName: string, attrId: number) {
  const arr = selectedDimensions[groupName] ??= []
  const idx = arr.indexOf(attrId)
  if (idx === -1) arr.push(attrId)
  else arr.splice(idx, 1)
}

function toggleValue(attrId: number, value: string) {
  const arr = selectedValues[attrId] ??= []
  const idx = arr.indexOf(value)
  if (idx === -1) arr.push(value)
  else arr.splice(idx, 1)
}

function isValueSelected(attrId: number, value: string) {
  return (selectedValues[attrId] ?? []).includes(value)
}

function generateSkus() {
  const dimensions: { attrName: string; values: string[] }[] = []
  for (const g of skuDimensionGroups.value) {
    for (const attrId of selectedDimensions[g.groupName!] ?? []) {
      const vals = selectedValues[attrId]
      if (vals?.length) {
        const attr = g.attributes.find(a => a.attributeId === attrId)
        if (attr) dimensions.push({ attrName: attr.name, values: vals })
      }
    }
  }
  if (dimensions.length === 0) { errorMsg.value = '请至少选择一个规格维度并勾选规格值'; return }

  const combos = cartesian(dimensions)
  form.skus = combos.map(combo => ({
    skuCode: '',
    barcode: '',
    saleAttributes: { ...combo },
    price: 0,
    stock: 0,
  }))
  errorMsg.value = ''
}

function cartesian(dims: { attrName: string; values: string[] }[]): Record<string, string>[] {
  if (dims.length === 0) return [{}]
  const [first, ...rest] = dims
  return first.values.flatMap(v => cartesian(rest).map(c => ({ [first.attrName]: v, ...c })))
}

// ── Images ────────────────────────────────────────────────────────────────────

function addImage() {
  const url = newImageUrl.value.trim()
  if (url && form.images.length < 8) { form.images.push(url); newImageUrl.value = '' }
}

// ── Save ──────────────────────────────────────────────────────────────────────

async function save() {
  errorMsg.value = ''
  if (!form.productName.trim()) { errorMsg.value = '请输入商品名称'; return }
  if (!selectedCategoryId.value) { errorMsg.value = '请选择商品类目'; return }

  saving.value = true
  try {
    const skus: CreateSkuRequest[] = form.skus.map(s => ({
      skuCode: s.skuCode,
      barcode: s.barcode,
      saleAttributes: s.saleAttributes,
      price: s.price,
      costPrice: s.costPrice,
      stock: s.stock,
    }))

    const payload = {
      categoryId: selectedCategoryId.value,
      productName: form.productName,
      description: form.description || undefined,
      mainImage: form.mainImage || undefined,
      attributes: Object.keys(form.attributes).length ? { ...form.attributes } : undefined,
      skus: skus.length ? skus : undefined,
      images: form.images.length ? [...form.images] : undefined,
      vectorizeAfterSave: form.vectorizeAfterSave,
    }

    if (isEdit.value) {
      await updateProduct(editId.value, payload)
    } else {
      await createProduct(payload)
    }
    router.push('/plm/product')
  } catch (e: unknown) {
    errorMsg.value = (e as Error).message || '保存失败'
  } finally {
    saving.value = false
  }
}

// ── Load (edit mode) ──────────────────────────────────────────────────────────

async function loadProduct() {
  const product = await getProductById(editId.value)
  const path = findAncestorPath(categoryOptions.value, product.categoryId)
  selectedCategoryId.value = path.at(-1) ?? 0
  if (selectedCategoryId.value) await loadAttributes(selectedCategoryId.value)

  form.productName = product.productName
  form.description = product.description ?? ''
  form.mainImage = product.mainImage ?? ''
  form.attributes = { ...product.attributes }
  form.images = [...product.images]
  form.skus = product.skus.map(s => ({
    skuCode: s.skuCode,
    barcode: s.barcode ?? undefined,
    saleAttributes: { ...s.saleAttributes },
    price: s.price,
    costPrice: s.costPrice ?? undefined,
    stock: s.stock,
  }))
}

// ── Review timeline ───────────────────────────────────────────────────────────

function reviewTimelineColor(action: string): string {
  if (action === 'approve' || action === 'publish') return 'var(--success)'
  if (action === 'reject') return 'var(--danger)'
  if (action === 'discontinue') return 'var(--warning)'
  if (action === 'submit') return 'var(--primary)'
  return 'var(--muted-foreground)'
}

function formatTime(val: string) {
  if (!val) return '-'
  const d = new Date(val)
  return Number.isNaN(d.getTime()) ? val : d.toLocaleString('zh-CN', { timeZone: 'Asia/Shanghai', hour12: false })
}

onMounted(async () => {
  try { categoryOptions.value = await getCategoryTree() } catch { /* ignore */ }
  if (isEdit.value) {
    try { await loadProduct() } catch { /* ignore */ }
    try { reviewHistory.value = await getProductReviews(editId.value) } catch { /* ignore */ }
  } else {
    form.skus = [{ skuCode: '', price: 0, stock: 0 }]
  }
})
</script>

<template>
  <div class="max-w-3xl space-y-4 pb-8">
    <!-- Page title -->
    <div class="flex items-center justify-between">
      <h2 class="text-[16px] font-semibold text-foreground">
        {{ isEdit ? '编辑物料' : '新增物料' }}
      </h2>
      <div class="flex gap-2">
        <ButtonVue variant="outline" size="sm" @click="router.back()">取消</ButtonVue>
        <ButtonVue size="sm" :disabled="saving" @click="save">
          {{ saving ? '保存中...' : (isEdit ? '保存修改' : '创建物料') }}
        </ButtonVue>
      </div>
    </div>

    <!-- Error banner -->
    <div v-if="errorMsg" class="px-4 py-3 bg-[color-mix(in_oklch,var(--danger)_12%,transparent)] text-[color:var(--danger)] rounded-[var(--radius)] text-[12px] border border-[color-mix(in_oklch,var(--danger)_30%,transparent)]">
      {{ errorMsg }}
    </div>

    <!-- ── 基本信息 ─────────────────────────────────────────────────────── -->
    <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
      <div class="px-4 py-3 border-b border-border text-[13px] font-semibold">基本信息</div>
      <div class="p-4 space-y-4">

        <!-- Category -->
        <div class="space-y-1">
          <label class="text-[12px] font-medium">商品类目 <span class="text-[color:var(--danger)]">*</span></label>
          <select
            :value="selectedCategoryId"
            class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            @change="onCategoryChange(Number(($event.target as HTMLSelectElement).value))"
          >
            <option :value="0" disabled>请选择类目</option>
            <option
              v-for="{ node, depth } in flatCategories"
              :key="node.categoryId"
              :value="node.categoryId"
            >{{ catIndent(depth) }}{{ node.name }}</option>
          </select>
        </div>

        <!-- Product name -->
        <div class="space-y-1">
          <label class="text-[12px] font-medium">物料名称 <span class="text-[color:var(--danger)]">*</span></label>
          <InputVue v-model="form.productName" placeholder="请输入物料名称" />
        </div>

        <!-- Description -->
        <div class="space-y-1">
          <label class="text-[12px] font-medium">描述</label>
          <textarea
            v-model="form.description"
            rows="3"
            placeholder="物料描述（可选）"
            class="w-full rounded-[var(--radius)] border border-input bg-card px-3 py-2 text-[12px] text-foreground resize-y focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring placeholder:text-muted-foreground"
          />
        </div>

        <!-- Main image -->
        <div class="space-y-1">
          <label class="text-[12px] font-medium">主图</label>
          <div class="flex gap-3 items-start">
            <!-- Preview -->
            <div class="relative flex-shrink-0 w-24 h-24 rounded-[var(--radius)] border border-dashed border-border bg-subtle flex items-center justify-center text-muted-foreground overflow-hidden">
              <img
                v-if="form.mainImage"
                :src="form.mainImage"
                class="w-full h-full object-cover"
                alt="主图预览"
                @error="($event.target as HTMLImageElement).style.display='none'"
              />
              <ImageIcon v-else :size="24" class="opacity-40" />
              <button
                v-if="form.mainImage"
                type="button"
                class="absolute top-0.5 right-0.5 w-5 h-5 rounded-full bg-black/60 text-white flex items-center justify-center cursor-pointer border-0 p-0"
                @click="form.mainImage = ''"
              ><X :size="11" /></button>
            </div>
            <!-- Upload controls -->
            <div class="flex-1 flex flex-col gap-2">
              <input ref="mainImageInputRef" type="file" accept="image/jpeg,image/png,image/webp,image/gif" class="hidden" @change="onMainImageFile" />
              <ButtonVue variant="outline" size="sm" :disabled="mainImageUploading" @click="mainImageInputRef?.click()">
                <Upload :size="13" />
                {{ mainImageUploading ? '上传中...' : '选择图片' }}
              </ButtonVue>
              <InputVue v-model="form.mainImage" placeholder="或直接输入图片 URL" />
            </div>
          </div>
        </div>

        <!-- Vectorize -->
        <label class="flex items-center gap-2 cursor-pointer select-none">
          <input
            v-model="form.vectorizeAfterSave"
            type="checkbox"
            class="w-4 h-4 rounded border-input accent-primary cursor-pointer"
          />
          <span class="text-[12px] text-muted-foreground">保存后自动将主图入 Qdrant 向量库（用于以图搜商品）</span>
        </label>
      </div>
    </div>

    <!-- ── 基础属性 ─────────────────────────────────────────────────────── -->
    <div v-if="spuAttributes.length > 0" class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
      <div class="px-4 py-3 border-b border-border text-[13px] font-semibold">基础属性</div>
      <div class="p-4 grid grid-cols-2 gap-4">
        <div v-for="attr in spuAttributes" :key="attr.attributeId" class="space-y-1">
          <label class="text-[12px] font-medium">
            {{ attr.name }}
            <span v-if="attr.isRequired" class="text-[color:var(--danger)]"> *</span>
            <span v-if="attr.unit" class="text-muted-foreground font-normal">（{{ attr.unit }}）</span>
          </label>
          <select
            v-if="attr.attributeType === 'enum' && attr.values.length > 0"
            v-model="form.attributes[attr.name]"
            class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <option value="">请选择</option>
            <option v-for="v in attr.values" :key="v.valueId" :value="v.value">{{ v.label }}</option>
          </select>
          <InputVue v-else v-model="form.attributes[attr.name]" :placeholder="'请输入 ' + attr.name" />
        </div>
      </div>
    </div>

    <!-- ── SKU 规格 ─────────────────────────────────────────────────────── -->
    <div v-if="skuDimensionGroups.length > 0" class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
      <div class="px-4 py-3 border-b border-border text-[13px] font-semibold">SKU 规格维度</div>
      <div class="p-4 space-y-4">
        <div v-for="group in skuDimensionGroups" :key="group.groupName" class="space-y-2">
          <div class="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">{{ group.groupLabel }}</div>

          <!-- Attribute checkboxes -->
          <div class="flex flex-wrap gap-2">
            <label
              v-for="attr in group.attributes"
              :key="attr.attributeId"
              class="flex items-center gap-1.5 cursor-pointer"
            >
              <input
                type="checkbox"
                :checked="(selectedDimensions[group.groupName!] ?? []).includes(attr.attributeId)"
                class="w-3.5 h-3.5 accent-primary cursor-pointer"
                @change="toggleDimension(group.groupName!, attr.attributeId)"
              />
              <span class="text-[12px]">{{ attr.name }}</span>
            </label>
          </div>

          <!-- Value checkboxes for selected attributes -->
          <div
            v-for="attrId in (selectedDimensions[group.groupName!] ?? [])"
            :key="attrId"
            class="pl-3 border-l-2 border-border"
          >
            <div class="text-[11px] text-muted-foreground mb-1">
              {{ group.attributes.find(a => a.attributeId === attrId)?.name }} 的值：
            </div>
            <div class="flex flex-wrap gap-2">
              <label
                v-for="v in group.attributes.find(a => a.attributeId === attrId)?.values ?? []"
                :key="v.valueId"
                class="flex items-center gap-1.5 cursor-pointer"
              >
                <input
                  type="checkbox"
                  :checked="isValueSelected(attrId, v.value)"
                  class="w-3.5 h-3.5 accent-primary cursor-pointer"
                  @change="toggleValue(attrId, v.value)"
                />
                <span class="text-[12px]">{{ v.label }}</span>
              </label>
            </div>
          </div>
        </div>

        <ButtonVue variant="outline" size="sm" @click="generateSkus">
          生成 SKU 组合
        </ButtonVue>
      </div>
    </div>

    <!-- ── SKU 列表 ─────────────────────────────────────────────────────── -->
    <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
      <div class="px-4 py-3 border-b border-border flex items-center justify-between">
        <span class="text-[13px] font-semibold">
          SKU 信息
          <span class="text-[11px] font-normal text-muted-foreground ml-1">（{{ form.skus.length }} 条）</span>
        </span>
        <ButtonVue
          v-if="skuDimensionGroups.length === 0"
          variant="outline"
          size="sm"
          @click="form.skus.push({ skuCode: '', price: 0, stock: 0 })"
        >
          <Plus :size="13" />
          添加
        </ButtonVue>
      </div>
      <div class="overflow-x-auto">
        <table class="w-full text-[12px]">
          <thead>
            <tr>
              <th
                v-if="skuDimensionGroups.length > 0"
                class="text-left text-[11px] uppercase tracking-wide text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border"
              >规格</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border w-[140px]">SKU 编码</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border w-[120px]">条码</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border w-[110px]">价格</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border w-[110px]">成本价</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border w-[90px]">库存</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border w-[50px]"></th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="(sku, i) in form.skus"
              :key="i"
              class="border-b border-border last:border-0 hover:bg-subtle/50"
            >
              <!-- Sale attributes badges -->
              <td v-if="skuDimensionGroups.length > 0" class="px-3 py-2">
                <div class="flex flex-wrap gap-1">
                  <BadgeVue
                    v-for="(val, key) in sku.saleAttributes"
                    :key="key"
                    variant="info"
                    class="text-[10px]"
                  >{{ key }}: {{ val }}</BadgeVue>
                </div>
              </td>
              <td class="px-3 py-1.5">
                <input
                  v-model="sku.skuCode"
                  placeholder="SKU 编码"
                  class="w-full h-7 rounded border border-input bg-background px-2 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                />
              </td>
              <td class="px-3 py-1.5">
                <input
                  v-model="sku.barcode"
                  placeholder="条码"
                  class="w-full h-7 rounded border border-input bg-background px-2 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                />
              </td>
              <td class="px-3 py-1.5">
                <input
                  v-model.number="sku.price"
                  type="number"
                  min="0"
                  step="0.01"
                  placeholder="0.00"
                  class="w-full h-7 rounded border border-input bg-background px-2 text-[12px] font-mono-tnum text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                />
              </td>
              <td class="px-3 py-1.5">
                <input
                  v-model.number="sku.costPrice"
                  type="number"
                  min="0"
                  step="0.01"
                  placeholder="0.00"
                  class="w-full h-7 rounded border border-input bg-background px-2 text-[12px] font-mono-tnum text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                />
              </td>
              <td class="px-3 py-1.5">
                <input
                  v-model.number="sku.stock"
                  type="number"
                  min="0"
                  placeholder="0"
                  class="w-full h-7 rounded border border-input bg-background px-2 text-[12px] font-mono-tnum text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                />
              </td>
              <td class="px-3 py-1.5 text-center">
                <button
                  v-if="form.skus.length > 1"
                  type="button"
                  class="text-muted-foreground hover:text-danger cursor-pointer border-0 bg-transparent p-0"
                  @click="form.skus.splice(i, 1)"
                >
                  <X :size="13" />
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- ── 商品图片 ─────────────────────────────────────────────────────── -->
    <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
      <div class="px-4 py-3 border-b border-border text-[13px] font-semibold">
        商品图片
        <span class="text-[11px] font-normal text-muted-foreground ml-1">（最多 8 张）</span>
      </div>
      <div class="p-4 space-y-3">
        <div class="flex flex-wrap gap-2">
          <div
            v-for="(url, i) in form.images"
            :key="i"
            class="relative w-20 h-20 rounded-[var(--radius)] overflow-hidden border border-border bg-subtle group"
          >
            <img :src="url" class="w-full h-full object-cover" alt="商品图" />
            <button
              type="button"
              class="absolute top-0.5 right-0.5 w-5 h-5 rounded-full bg-black/60 text-white flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity cursor-pointer border-0 p-0"
              @click="form.images.splice(i, 1)"
            >
              <X :size="11" />
            </button>
          </div>
          <div
            v-if="form.images.length === 0"
            class="w-20 h-20 rounded-[var(--radius)] border border-dashed border-border bg-subtle flex items-center justify-center text-muted-foreground"
          >
            <ImageIcon :size="20" />
          </div>
        </div>
        <div v-if="form.images.length < 8" class="flex gap-2">
          <input ref="galleryInputRef" type="file" accept="image/jpeg,image/png,image/webp,image/gif" class="hidden" @change="onGalleryFile" />
          <ButtonVue variant="outline" size="sm" :disabled="galleryUploading" @click="galleryInputRef?.click()">
            <Upload :size="13" />
            {{ galleryUploading ? '上传中...' : '上传图片' }}
          </ButtonVue>
          <InputVue
            v-model="newImageUrl"
            placeholder="或输入图片 URL 回车添加"
            class="flex-1"
            @keyup.enter="addImage"
          />
          <ButtonVue variant="outline" size="sm" @click="addImage">添加</ButtonVue>
        </div>
      </div>
    </div>

    <!-- ── 审核历史 ─────────────────────────────────────────────────────── -->
    <div v-if="isEdit && reviewHistory.length > 0" class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
      <div class="px-4 py-3 border-b border-border text-[13px] font-semibold">审核历史</div>
      <div class="p-4">
        <div class="relative pl-5">
          <!-- Vertical line -->
          <div class="absolute left-[7px] top-2 bottom-2 w-px bg-border" />

          <div
            v-for="(item, i) in reviewHistory"
            :key="item.reviewId"
            class="relative mb-4 last:mb-0"
          >
            <!-- Dot -->
            <div
              class="absolute -left-5 top-0.5 w-3.5 h-3.5 rounded-full border-2 border-background"
              :style="{ backgroundColor: reviewTimelineColor(item.action) }"
            />
            <div class="space-y-0.5">
              <div class="flex items-center gap-2">
                <span class="text-[13px] font-medium">{{ item.actionLabel }}</span>
                <span class="text-[11px] text-muted-foreground">{{ item.reviewerName }}</span>
              </div>
              <div class="text-[11px] text-muted-foreground">{{ formatTime(item.createdAt) }}</div>
              <div v-if="item.comment" class="text-[12px] text-muted-foreground mt-0.5 bg-subtle px-2 py-1 rounded">
                {{ item.comment }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ── Bottom actions ──────────────────────────────────────────────── -->
    <div class="flex justify-end gap-2 pt-2">
      <ButtonVue variant="outline" @click="router.back()">取消</ButtonVue>
      <ButtonVue :disabled="saving" @click="save">
        {{ saving ? '保存中...' : (isEdit ? '保存修改' : '创建物料') }}
      </ButtonVue>
    </div>
  </div>
</template>
