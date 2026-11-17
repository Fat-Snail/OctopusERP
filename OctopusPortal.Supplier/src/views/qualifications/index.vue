<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft, FileBadge2, UploadCloud } from '@lucide/vue'
import { portalApi } from '@/api/mock'
import type { Qualification } from '@/api/types'

const router = useRouter()
const quals = ref<Qualification[]>([])
const loading = ref(true)
const uploading = ref(false)

// 上传表单
const title = ref('')
const category = ref('基础资质')
const fileInput = ref<HTMLInputElement | null>(null)
const pickedFile = ref<File | null>(null)
const errorMsg = ref('')

const categories = ['基础资质', '质量认证', '环保认证', '其他']

onMounted(async () => {
  quals.value = await portalApi.getQualifications()
  loading.value = false
})

const statusMeta: Record<Qualification['status'], { label: string; cls: string }> = {
  approved: { label: '已认证', cls: 'text-success bg-success/10' },
  pending: { label: '审核中', cls: 'text-warning bg-warning/10' },
  expired: { label: '已过期', cls: 'text-danger bg-danger/10' },
}

function pickFile() {
  fileInput.value?.click()
}

function onFilePicked(e: Event) {
  const files = (e.target as HTMLInputElement).files
  if (files && files.length > 0) {
    pickedFile.value = files[0]!
    if (!title.value) {
      title.value = pickedFile.value.name.replace(/\.[^.]+$/, '')
    }
  }
}

async function onUpload() {
  errorMsg.value = ''
  if (!title.value.trim()) { errorMsg.value = '请填写资质名称'; return }
  if (!pickedFile.value) { errorMsg.value = '请选择要上传的文件'; return }

  uploading.value = true
  try {
    // Mock：本地构造记录；接入真实 API 后走文件流上传至对象存储/网关
    const created = await portalApi.uploadQualification({
      title: title.value.trim(),
      category: category.value,
      fileName: pickedFile.value.name,
    })
    quals.value = [created, ...quals.value]
    title.value = ''
    category.value = '基础资质'
    pickedFile.value = null
    if (fileInput.value) fileInput.value.value = ''
  } finally {
    uploading.value = false
  }
}
</script>

<template>
  <div class="min-h-dvh bg-subtle">
    <header class="sticky top-0 z-10 bg-background px-5 pt-6 pb-3 border-b border-border flex items-center gap-3">
      <button class="border-0 bg-transparent cursor-pointer p-1 -ml-1" @click="router.back()">
        <ArrowLeft :size="20" />
      </button>
      <h1 class="text-lg font-semibold">资质上传</h1>
    </header>

    <div v-if="loading" class="px-5 py-10 text-center text-muted-foreground text-sm">加载中…</div>

    <template v-else>
      <!-- Upload form -->
      <div class="px-5 pt-4">
        <div class="bg-card rounded-xl p-4 shadow-sm">
          <button
            class="w-full border border-dashed border-border rounded-xl py-6 flex flex-col items-center gap-2 cursor-pointer bg-background active:opacity-80 border-0"
            @click="pickFile"
          >
            <UploadCloud :size="26" class="text-primary" />
            <p class="text-sm">{{ pickedFile ? pickedFile.name : '点击选择文件（PDF / 图片）' }}</p>
            <p class="text-[11px] text-muted-foreground">营业执照 / 认证证书 / 检测报告</p>
          </button>
          <input ref="fileInput" type="file" class="hidden" @change="onFilePicked" />

          <div class="grid grid-cols-2 gap-3 mt-4">
            <div>
              <label class="text-xs text-muted-foreground" for="qtitle">资质名称 *</label>
              <input
                id="qtitle"
                v-model="title"
                type="text"
                placeholder="如：ISO 9001 认证"
                class="mt-1.5 w-full h-10 rounded-lg border border-border bg-background px-3 text-sm outline-none focus:border-primary"
              />
            </div>
            <div>
              <label class="text-xs text-muted-foreground" for="qcat">类别</label>
              <select
                id="qcat"
                v-model="category"
                class="mt-1.5 w-full h-10 rounded-lg border border-border bg-background px-2 text-sm outline-none focus:border-primary"
              >
                <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
              </select>
            </div>
          </div>

          <p v-if="errorMsg" class="text-danger text-xs mt-3">{{ errorMsg }}</p>

          <button
            :disabled="uploading"
            class="mt-4 w-full h-11 rounded-xl bg-primary text-primary-foreground text-sm font-medium cursor-pointer border-0 active:opacity-80 disabled:opacity-60 flex items-center justify-center gap-2"
            @click="onUpload"
          >
            <FileBadge2 :size="16" />
            {{ uploading ? '上传中…' : '上传资质' }}
          </button>
        </div>
      </div>

      <!-- List -->
      <div class="px-5 mt-5 space-y-2.5">
        <h2 class="text-sm font-semibold">已上传资质</h2>
        <div
          v-for="q in quals"
          :key="q.id"
          class="bg-card rounded-xl p-3.5 shadow-sm"
        >
          <div class="flex items-start justify-between gap-2">
            <div class="min-w-0">
              <p class="text-sm font-medium truncate">{{ q.title }}</p>
              <p class="text-[11px] text-muted-foreground mt-0.5 truncate">
                {{ q.category }} · {{ q.fileName }} · {{ q.fileSize }}
              </p>
              <p class="text-[11px] text-muted-foreground mt-0.5">
                上传 {{ q.uploadedAt }}<template v-if="q.validUntil"> · 有效期至 {{ q.validUntil }}</template>
              </p>
            </div>
            <span :class="['text-[11px] px-2 py-0.5 rounded-full flex-none', statusMeta[q.status].cls]">
              {{ statusMeta[q.status].label }}
            </span>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
