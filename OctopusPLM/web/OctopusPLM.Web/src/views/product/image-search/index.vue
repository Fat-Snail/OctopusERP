<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Upload, ScanSearch, X, ArrowLeft, Loader2, Info, AlertTriangle } from 'lucide-vue-next'
import ButtonVue from '@/components/ui/button.vue'
import BadgeVue from '@/components/ui/badge.vue'
import { searchByImage } from '@/api/product'
import type { ImageSearchItem } from '@/api/product/types'

const router = useRouter()
const modelLoaded = ref(true)

onMounted(async () => {
  try {
    const res = await fetch('/api/vector/model/status')
    const data = await res.json() as { modelLoaded: boolean }
    modelLoaded.value = data.modelLoaded
  } catch {
    // ignore — assume loaded if API unreachable
  }
})

const dragging = ref(false)
const previewUrl = ref<string | null>(null)
const selectedFile = ref<File | null>(null)
const loading = ref(false)
const results = ref<ImageSearchItem[]>([])
const queryDescription = ref('')
const searched = ref(false)
const errorMsg = ref('')

function onDragOver(e: DragEvent) {
  e.preventDefault()
  dragging.value = true
}

function onDragLeave() {
  dragging.value = false
}

function onDrop(e: DragEvent) {
  e.preventDefault()
  dragging.value = false
  const file = e.dataTransfer?.files?.[0]
  if (file) selectFile(file)
}

function onFileInput(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (file) selectFile(file)
}

function selectFile(file: File) {
  if (!file.type.startsWith('image/')) {
    errorMsg.value = '请上传图片文件（JPG / PNG / WEBP）'
    return
  }
  errorMsg.value = ''
  selectedFile.value = file
  results.value = []
  queryDescription.value = ''
  searched.value = false
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = URL.createObjectURL(file)
}

function clearImage() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = null
  selectedFile.value = null
  results.value = []
  queryDescription.value = ''
  searched.value = false
  errorMsg.value = ''
}

async function doSearch() {
  if (!selectedFile.value) return
  loading.value = true
  errorMsg.value = ''
  queryDescription.value = ''
  try {
    const res = await searchByImage(selectedFile.value, 12)
    queryDescription.value = res.queryDescription
    results.value = res.items
    searched.value = true
  } catch (e: unknown) {
    errorMsg.value = (e as Error).message || '搜索失败，请检查 Ollama / Qdrant 服务是否运行'
  } finally {
    loading.value = false
  }
}

function scoreVariant(score: number): 'success' | 'info' | 'warning' {
  if (score >= 0.60) return 'success'
  if (score >= 0.35) return 'info'
  return 'warning'
}

function scoreLabel(score: number) {
  return (score * 100).toFixed(0) + '%'
}

function formatPrice(min: number, max: number) {
  if (min <= 0 && max <= 0) return '未设置'
  if (min === max) return `¥${min.toFixed(2)}`
  return `¥${min.toFixed(2)} ~ ¥${max.toFixed(2)}`
}
</script>

<template>
  <div class="space-y-6">
    <!-- Back + title -->
    <div class="flex items-center gap-3">
      <button
        class="inline-flex items-center gap-1 text-[12px] text-muted-foreground hover:text-foreground transition-colors cursor-pointer border-0 bg-transparent p-0"
        @click="router.push('/plm/product')"
      >
        <ArrowLeft :size="14" />
        返回列表
      </button>
      <span class="text-muted-foreground">/</span>
      <span class="text-[14px] font-semibold flex items-center gap-1.5">
        <ScanSearch :size="16" class="text-primary" />
        以图搜商品
      </span>
    </div>

    <!-- Model not loaded warning -->
    <div
      v-if="!modelLoaded"
      class="flex items-center gap-3 rounded-[var(--radius)] border border-warning/30 bg-warning/8 px-4 py-3"
    >
      <AlertTriangle :size="15" class="text-warning shrink-0" />
      <p class="text-[12px] text-foreground flex-1">
        CLIP 模型尚未加载，图片搜索功能不可用。
      </p>
      <button
        class="text-[12px] text-primary underline-offset-2 underline cursor-pointer border-0 bg-transparent p-0 shrink-0"
        @click="router.push('/plm/system/model')"
      >
        前往下载
      </button>
    </div>

    <!-- Upload zone + results panel -->
    <div class="grid grid-cols-1 lg:grid-cols-[360px_1fr] gap-6 items-start">
      <!-- Left: upload -->
      <div class="space-y-3">
        <!-- Drop zone -->
        <div
          v-if="!previewUrl"
          class="border-2 border-dashed rounded-[var(--radius-lg)] flex flex-col items-center justify-center gap-3 p-10 cursor-pointer transition-colors select-none"
          :class="dragging ? 'border-primary bg-primary-soft' : 'border-border hover:border-primary/50 bg-card'"
          @dragover="onDragOver"
          @dragleave="onDragLeave"
          @drop="onDrop"
          @click="($refs.fileInput as HTMLInputElement).click()"
        >
          <Upload :size="32" class="text-muted-foreground" />
          <div class="text-center">
            <p class="text-[13px] text-foreground font-medium">拖拽图片到此处</p>
            <p class="text-[11px] text-muted-foreground mt-1">或点击选择图片（JPG / PNG / WEBP）</p>
          </div>
          <input
            ref="fileInput"
            type="file"
            accept="image/*"
            class="hidden"
            @change="onFileInput"
          />
        </div>

        <!-- Preview -->
        <div v-else class="relative border border-border rounded-[var(--radius-lg)] overflow-hidden bg-card">
          <img
            :src="previewUrl"
            alt="搜索图片"
            class="w-full object-contain max-h-[320px]"
          />
          <button
            class="absolute top-2 right-2 w-6 h-6 rounded-full bg-black/50 text-white flex items-center justify-center hover:bg-black/70 transition-colors cursor-pointer border-0 p-0"
            @click="clearImage"
          >
            <X :size="12" />
          </button>
        </div>

        <!-- Error -->
        <p v-if="errorMsg" class="text-[12px] text-danger">{{ errorMsg }}</p>

        <!-- Search button -->
        <ButtonVue
          class="w-full"
          :disabled="!selectedFile || loading"
          @click="doSearch"
        >
          <Loader2 v-if="loading" :size="13" class="animate-spin mr-1" />
          <ScanSearch v-else :size="13" class="mr-1" />
          {{ loading ? 'AI 分析中，请稍候...' : '搜索相似商品' }}
        </ButtonVue>

        <!-- AI description of uploaded image -->
        <div
          v-if="queryDescription"
          class="rounded-[var(--radius)] bg-subtle border border-border p-3 space-y-1"
        >
          <p class="text-[11px] font-medium text-muted-foreground flex items-center gap-1">
            <Info :size="12" />
            AI 识别到的图片内容
          </p>
          <p class="text-[12px] text-foreground leading-relaxed">{{ queryDescription }}</p>
        </div>

        <!-- Hint -->
        <p v-if="!queryDescription" class="text-[11px] text-muted-foreground leading-relaxed">
          使用 CLIP 视觉模型直接提取图片特征，搜索速度约 1–3 秒。<br/>
          若无结果，请先到商品列表为商品建立向量索引。
        </p>
      </div>

      <!-- Right: results -->
      <div class="min-h-[200px]">
        <!-- Loading skeleton -->
        <div v-if="loading" class="space-y-3">
          <div
            v-for="i in 4"
            :key="i"
            class="h-[88px] rounded-[var(--radius-lg)] bg-subtle animate-pulse border border-border"
          />
        </div>

        <!-- Empty state: not searched yet -->
        <div
          v-else-if="!searched"
          class="flex flex-col items-center justify-center h-[240px] text-muted-foreground text-[12px] gap-2 border border-dashed border-border rounded-[var(--radius-lg)]"
        >
          <ScanSearch :size="32" class="opacity-30" />
          <p>上传图片后点击搜索，AI 将返回相似商品</p>
        </div>

        <!-- No results: searched but nothing passed threshold -->
        <div
          v-else-if="results.length === 0"
          class="flex flex-col items-center justify-center gap-3 border border-dashed border-border rounded-[var(--radius-lg)] p-8"
        >
          <ScanSearch :size="32" class="opacity-30 text-muted-foreground" />
          <div class="text-center space-y-1">
            <p class="text-[13px] text-foreground font-medium">未找到足够相似的商品</p>
            <p class="text-[11px] text-muted-foreground max-w-[320px] leading-relaxed">
              未找到足够相似的商品。可能是该类目商品尚未建立向量索引，
              请到商品列表进行「向量化」后再试。
            </p>
          </div>
        </div>

        <!-- Results list -->
        <div v-else class="space-y-3">
          <p class="text-[12px] text-muted-foreground">
            找到 <span class="text-foreground font-medium">{{ results.length }}</span> 个相似商品
          </p>
          <div
            v-for="(hit, idx) in results"
            :key="hit.product.productId"
            class="flex items-start gap-3 p-3 border border-border rounded-[var(--radius-lg)] bg-card hover:border-primary/40 hover:bg-subtle transition-colors cursor-pointer"
            @click="router.push(`/plm/product/edit/${hit.product.productId}`)"
          >
            <!-- Rank -->
            <div class="shrink-0 w-6 h-6 rounded-full bg-subtle border border-border flex items-center justify-center text-[11px] font-medium text-muted-foreground">
              {{ idx + 1 }}
            </div>

            <!-- Product image -->
            <div class="shrink-0 w-16 h-16 rounded-[var(--radius)] bg-subtle border border-border overflow-hidden flex items-center justify-center">
              <img
                v-if="hit.product.mainImage"
                :src="hit.product.mainImage"
                :alt="hit.product.productName"
                class="w-full h-full object-cover"
              />
              <span v-else class="text-[10px] text-muted-foreground">无图</span>
            </div>

            <!-- Info -->
            <div class="flex-1 min-w-0 space-y-1">
              <div class="flex items-center gap-2 flex-wrap">
                <span class="text-[13px] font-medium text-foreground truncate max-w-[280px]">
                  {{ hit.product.productName }}
                </span>
                <BadgeVue :variant="scoreVariant(hit.score)" class="shrink-0">
                  相似度 {{ scoreLabel(hit.score) }}
                </BadgeVue>
              </div>
              <div class="flex items-center gap-3 text-[11px] text-muted-foreground flex-wrap">
                <span>{{ hit.product.categoryName }}</span>
                <span class="font-mono-tnum">{{ formatPrice(hit.product.minPrice, hit.product.maxPrice) }}</span>
                <span>库存 {{ hit.product.totalStock }}</span>
              </div>
              <p
                v-if="hit.imageDescription"
                class="text-[11px] text-muted-foreground line-clamp-2 leading-relaxed"
                :title="hit.imageDescription"
              >
                商品描述：{{ hit.imageDescription }}
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
