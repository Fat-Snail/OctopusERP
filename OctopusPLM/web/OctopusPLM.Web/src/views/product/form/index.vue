<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import ButtonVue from '@/components/ui/button.vue'
import InputVue from '@/components/ui/input.vue'
import { getCategoryTree } from '@/api/category'
import { getProductById, createProduct, updateProduct } from '@/api/product'
import type { CategoryTreeNode } from '@/api/category/types'
import type { CreateSkuRequest } from '@/api/product/types'

const router = useRouter()
const route = useRoute()

const isEdit = ref(route.name === 'PlmProductEdit')
const editId = ref(isEdit.value ? Number(route.params['id']) : 0)
const saving = ref(false)
const errorMsg = ref('')
const categoryOptions = ref<CategoryTreeNode[]>([])
const selectedCategoryId = ref<number>(0)

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
  skus: [{ skuCode: '', price: 0, stock: 0 }] as SkuRow[],
  images: [] as string[],
  status: 1,
  remark: '',
})

function flattenCategories(nodes: CategoryTreeNode[]): CategoryTreeNode[] {
  const result: CategoryTreeNode[] = []
  function walk(list: CategoryTreeNode[]) {
    for (const n of list) {
      result.push(n)
      if (n.children.length > 0) walk(n.children)
    }
  }
  walk(nodes)
  return result
}

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
      attributes: Object.keys(form.attributes).length > 0 ? { ...form.attributes } : undefined,
      skus: skus.length > 0 ? skus : undefined,
      images: form.images.length > 0 ? [...form.images] : undefined,
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

async function loadProduct() {
  const product = await getProductById(editId.value)
  const flat = flattenCategories(categoryOptions.value)
  const cat = flat.find(c => c.categoryId === product.categoryId)
  if (cat) selectedCategoryId.value = cat.categoryId

  form.productName = product.productName
  form.description = product.description || ''
  form.mainImage = product.mainImage || ''
  form.attributes = { ...product.attributes }
  form.images = [...product.images]
  form.skus = product.skus.length > 0
    ? product.skus.map(s => ({
        skuCode: s.skuCode,
        barcode: s.barcode || undefined,
        saleAttributes: { ...s.saleAttributes },
        price: s.price,
        costPrice: s.costPrice || undefined,
        stock: s.stock,
      }))
    : [{ skuCode: '', price: 0, stock: 0 }]
}

const statusOptions = [
  { value: 1, label: '启用' },
  { value: 0, label: '禁用' },
]

const unitOptions = ['个', '件', '套', '箱', '台', '米', '千克', '升']

onMounted(async () => {
  try {
    categoryOptions.value = await getCategoryTree()
  } catch { /* ignore */ }
  if (isEdit.value) {
    try { await loadProduct() } catch { /* ignore */ }
  }
})
</script>

<template>
  <div class="max-w-2xl space-y-5">
    <!-- Error message -->
    <div v-if="errorMsg" class="px-4 py-3 bg-danger/10 text-danger rounded-[var(--radius)] text-[12px]">
      {{ errorMsg }}
    </div>

    <!-- Form card -->
    <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
      <div class="px-5 py-3 border-b border-border text-[13px] font-semibold text-foreground">
        基本信息
      </div>
      <div class="p-5 space-y-4">
        <!-- Category -->
        <div class="space-y-1">
          <label class="text-[12px] font-medium text-foreground">商品类目 <span class="text-danger">*</span></label>
          <select
            v-model="selectedCategoryId"
            class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <option :value="0" disabled>请选择类目</option>
            <option
              v-for="cat in categoryOptions"
              :key="cat.categoryId"
              :value="cat.categoryId"
            >
              {{ cat.name }}
            </option>
          </select>
        </div>

        <!-- Product name -->
        <div class="space-y-1">
          <label class="text-[12px] font-medium text-foreground">物料名称 <span class="text-danger">*</span></label>
          <InputVue v-model="form.productName" placeholder="请输入物料名称" />
        </div>

        <!-- Description -->
        <div class="space-y-1">
          <label class="text-[12px] font-medium text-foreground">描述</label>
          <textarea
            v-model="form.description"
            rows="3"
            placeholder="物料描述"
            class="w-full rounded-[var(--radius)] border border-input bg-card px-3 py-2 text-[12px] text-foreground resize-y focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring placeholder:text-muted-foreground"
          />
        </div>

        <!-- Main image -->
        <div class="space-y-1">
          <label class="text-[12px] font-medium text-foreground">主图 URL</label>
          <InputVue v-model="form.mainImage" placeholder="图片 URL" />
        </div>

        <!-- Status -->
        <div class="space-y-1">
          <label class="text-[12px] font-medium text-foreground">状态</label>
          <select
            v-model="form.status"
            class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <option v-for="opt in statusOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>
      </div>
    </div>

    <!-- SKU card -->
    <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
      <div class="px-5 py-3 border-b border-border flex items-center justify-between">
        <span class="text-[13px] font-semibold text-foreground">SKU 信息</span>
        <ButtonVue
          variant="outline"
          size="sm"
          type="button"
          @click="form.skus.push({ skuCode: '', price: 0, stock: 0 })"
        >
          + 添加 SKU
        </ButtonVue>
      </div>
      <div class="p-5 space-y-3">
        <div
          v-for="(sku, i) in form.skus"
          :key="i"
          class="border border-border rounded-[var(--radius)] p-3 space-y-3"
        >
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-medium text-muted-foreground uppercase tracking-wide">SKU #{{ i + 1 }}</span>
            <button
              v-if="form.skus.length > 1"
              type="button"
              class="text-[11px] text-danger hover:underline cursor-pointer border-0 bg-transparent p-0"
              @click="form.skus.splice(i, 1)"
            >
              删除
            </button>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1">
              <label class="text-[11px] text-muted-foreground">SKU 编码</label>
              <InputVue v-model="sku.skuCode" placeholder="SKU 编码" />
            </div>
            <div class="space-y-1">
              <label class="text-[11px] text-muted-foreground">条码</label>
              <InputVue v-model="(sku as { barcode?: string }).barcode" placeholder="条码（可选）" />
            </div>
            <div class="space-y-1">
              <label class="text-[11px] text-muted-foreground">价格</label>
              <input
                v-model.number="sku.price"
                type="number"
                min="0"
                step="0.01"
                class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] font-mono-tnum text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                placeholder="0.00"
              />
            </div>
            <div class="space-y-1">
              <label class="text-[11px] text-muted-foreground">库存</label>
              <input
                v-model.number="sku.stock"
                type="number"
                min="0"
                class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] font-mono-tnum text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                placeholder="0"
              />
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Actions -->
    <div class="flex items-center gap-2 justify-end">
      <ButtonVue variant="outline" type="button" @click="router.back()">取消</ButtonVue>
      <ButtonVue :disabled="saving" type="button" @click="save">
        {{ saving ? '保存中...' : (isEdit ? '保存修改' : '创建物料') }}
      </ButtonVue>
    </div>
  </div>
</template>
