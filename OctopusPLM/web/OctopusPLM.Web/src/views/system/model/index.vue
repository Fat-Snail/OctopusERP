<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { Download, Cpu, CheckCircle, XCircle, Loader2, RefreshCw, AlertTriangle } from 'lucide-vue-next'
import ButtonVue from '@/components/ui/button.vue'
import BadgeVue from '@/components/ui/badge.vue'

interface ModelStatus {
  state: 'idle' | 'downloading' | 'done' | 'error'
  progressPct: number
  downloadedMb: number
  totalMb: number
  modelLoaded: boolean
  error: string | null
}

const status = ref<ModelStatus | null>(null)
const loading = ref(false)
let eventSource: EventSource | null = null

async function fetchStatus() {
  try {
    const res = await fetch('/api/vector/model/status')
    status.value = await res.json() as ModelStatus
  } catch {
    // ignore
  }
}

function startSSE() {
  if (eventSource) {
    eventSource.close()
    eventSource = null
  }
  eventSource = new EventSource('/api/vector/model/download/stream')
  eventSource.onmessage = (e: MessageEvent) => {
    const data = JSON.parse(e.data as string) as ModelStatus
    status.value = data
    if (data.state === 'done' || data.state === 'error') {
      eventSource?.close()
      eventSource = null
    }
  }
  eventSource.onerror = () => {
    eventSource?.close()
    eventSource = null
  }
}

async function triggerDownload(mirror: boolean) {
  loading.value = true
  try {
    await fetch(`/api/vector/model/download?mirror=${mirror}`, { method: 'POST' })
    startSSE()
  } catch {
    // ignore
  } finally {
    loading.value = false
  }
}

function formatMb(mb: number) {
  return mb.toFixed(1) + ' MB'
}

onMounted(async () => {
  await fetchStatus()
  if (status.value?.state === 'downloading') {
    startSSE()
  }
})

onUnmounted(() => {
  eventSource?.close()
  eventSource = null
})
</script>

<template>
  <div class="max-w-[640px] space-y-6">
    <!-- Page header -->
    <div>
      <h1 class="text-[18px] font-semibold flex items-center gap-2">
        <Cpu :size="18" class="text-primary" />
        CLIP 模型管理
      </h1>
      <p class="text-[12px] text-muted-foreground mt-1">
        管理「以图搜商品」所需的 CLIP ViT-B/32 视觉模型（约 85 MB）
      </p>
    </div>

    <!-- Status card -->
    <div class="border border-border rounded-[var(--radius-lg)] bg-card p-5 space-y-4">
      <div class="flex items-center justify-between">
        <span class="text-[13px] font-medium">模型状态</span>
        <button
          class="text-[11px] text-muted-foreground hover:text-foreground flex items-center gap-1 cursor-pointer border-0 bg-transparent p-0 transition-colors"
          @click="fetchStatus"
        >
          <RefreshCw :size="11" />
          刷新
        </button>
      </div>

      <div v-if="!status" class="flex items-center gap-2 text-[12px] text-muted-foreground">
        <Loader2 :size="13" class="animate-spin" />
        加载中...
      </div>

      <template v-else>
        <!-- Loaded -->
        <div v-if="status.modelLoaded" class="flex items-center gap-2.5">
          <CheckCircle :size="18" class="text-success shrink-0" />
          <div>
            <p class="text-[13px] font-medium text-foreground">模型已加载</p>
            <p class="text-[11px] text-muted-foreground">CLIP ViT-B/32 已就绪，以图搜商品功能可正常使用</p>
          </div>
          <BadgeVue variant="success" class="ml-auto shrink-0">已就绪</BadgeVue>
        </div>

        <!-- Not loaded -->
        <div v-else-if="status.state === 'idle' || status.state === 'error'" class="flex items-center gap-2.5">
          <XCircle :size="18" class="text-danger shrink-0" />
          <div class="flex-1 min-w-0">
            <p class="text-[13px] font-medium text-foreground">模型未加载</p>
            <p class="text-[11px] text-muted-foreground">需要下载模型文件后方可使用以图搜商品</p>
            <p v-if="status.error" class="text-[11px] text-danger mt-0.5 truncate">{{ status.error }}</p>
          </div>
          <BadgeVue variant="danger" class="ml-auto shrink-0">未就绪</BadgeVue>
        </div>

        <!-- Done but not yet loaded (shouldn't happen, but just in case) -->
        <div v-else-if="status.state === 'done'" class="flex items-center gap-2.5">
          <CheckCircle :size="18" class="text-success shrink-0" />
          <div>
            <p class="text-[13px] font-medium text-foreground">下载完成</p>
            <p class="text-[11px] text-muted-foreground">模型文件已下载，正在热加载...</p>
          </div>
        </div>

        <!-- Downloading -->
        <div v-else-if="status.state === 'downloading'" class="space-y-3">
          <div class="flex items-center gap-2.5">
            <Loader2 :size="18" class="text-primary animate-spin shrink-0" />
            <div class="flex-1">
              <p class="text-[13px] font-medium text-foreground">下载中...</p>
              <p class="text-[11px] text-muted-foreground">
                {{ formatMb(status.downloadedMb) }}
                <template v-if="status.totalMb > 0"> / {{ formatMb(status.totalMb) }}</template>
              </p>
            </div>
            <BadgeVue variant="info" class="shrink-0">{{ status.progressPct.toFixed(1) }}%</BadgeVue>
          </div>
          <!-- Progress bar -->
          <div class="h-1.5 rounded-full bg-muted overflow-hidden">
            <div
              class="h-full rounded-full bg-primary transition-all duration-300"
              :style="{ width: status.progressPct + '%' }"
            />
          </div>
        </div>
      </template>
    </div>

    <!-- Download section (only when not loaded and not downloading) -->
    <div
      v-if="status && !status.modelLoaded && status.state !== 'downloading' && status.state !== 'done'"
      class="border border-border rounded-[var(--radius-lg)] bg-card p-5 space-y-4"
    >
      <div>
        <p class="text-[13px] font-medium">下载模型</p>
        <p class="text-[12px] text-muted-foreground mt-0.5">
          模型来源：HuggingFace <span class="font-mono text-[11px]">Xenova/clip-vit-base-patch32</span>
        </p>
      </div>

      <div class="flex items-start gap-2.5 rounded-[var(--radius)] bg-warning/10 border border-warning/20 p-3">
        <AlertTriangle :size="13" class="text-warning shrink-0 mt-0.5" />
        <p class="text-[12px] text-foreground leading-relaxed">
          若访问 HuggingFace 速度慢，请使用镜像源（国内推荐）。
          下载完成后模型自动热加载，无需重启服务。
        </p>
      </div>

      <div class="flex gap-3">
        <ButtonVue
          :disabled="loading"
          class="flex-1"
          @click="triggerDownload(false)"
        >
          <Download :size="13" class="mr-1" />
          官方源下载
        </ButtonVue>
        <ButtonVue
          :disabled="loading"
          variant="outline"
          class="flex-1"
          @click="triggerDownload(true)"
        >
          <Download :size="13" class="mr-1" />
          镜像源下载（推荐）
        </ButtonVue>
      </div>
    </div>

    <!-- Model info -->
    <div class="border border-border rounded-[var(--radius-lg)] bg-card p-5 space-y-3">
      <p class="text-[13px] font-medium">模型说明</p>
      <div class="grid grid-cols-2 gap-x-6 gap-y-2 text-[12px]">
        <div class="flex items-center justify-between">
          <span class="text-muted-foreground">模型名称</span>
          <span class="font-mono text-[11px]">CLIP ViT-B/32</span>
        </div>
        <div class="flex items-center justify-between">
          <span class="text-muted-foreground">文件大小</span>
          <span class="font-mono-tnum text-[11px]">≈ 85 MB</span>
        </div>
        <div class="flex items-center justify-between">
          <span class="text-muted-foreground">格式</span>
          <span class="font-mono text-[11px]">ONNX</span>
        </div>
        <div class="flex items-center justify-between">
          <span class="text-muted-foreground">用途</span>
          <span class="text-[11px]">以图搜商品</span>
        </div>
      </div>
    </div>
  </div>
</template>
