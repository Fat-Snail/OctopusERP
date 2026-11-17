<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft, FileDown } from '@lucide/vue'
import { portalApi } from '@/api/mock'
import type { DocumentItem } from '@/api/types'

const router = useRouter()
const docs = ref<DocumentItem[]>([])
const loading = ref(true)

onMounted(async () => {
  docs.value = await portalApi.getDocuments()
  loading.value = false
})

function download(d: DocumentItem) {
  // Mock：生成占位文本文件；接入真实 API 后改为 OSS/网关文件流下载
  const blob = new Blob([`${d.title}\n（Mock 占位文件，接入真实 API 后下载原始文档）\n更新时间：${d.updatedAt}`], {
    type: 'text/plain',
  })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${d.title}.txt`
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div class="min-h-dvh bg-subtle">
    <header class="sticky top-0 z-10 bg-background px-5 pt-6 pb-3 border-b border-border flex items-center gap-3">
      <button class="border-0 bg-transparent cursor-pointer p-1 -ml-1" @click="router.back()">
        <ArrowLeft :size="20" />
      </button>
      <h1 class="text-lg font-semibold">文档下载</h1>
    </header>

    <div v-if="loading" class="px-5 py-10 text-center text-muted-foreground text-sm">加载中…</div>

    <div v-else class="px-5 pt-4 pb-8 space-y-2.5">
      <div
        v-for="d in docs"
        :key="d.id"
        class="bg-card rounded-xl p-3.5 shadow-sm flex items-center gap-3"
      >
        <div class="flex-1 min-w-0">
          <p class="text-sm font-medium truncate">{{ d.title }}</p>
          <p class="text-[11px] text-muted-foreground mt-0.5">
            {{ d.category }} · {{ d.size }} · 更新 {{ d.updatedAt }} · {{ d.downloads }} 次下载
          </p>
        </div>
        <button
          class="w-9 h-9 rounded-lg bg-primary-soft flex items-center justify-center border-0 cursor-pointer flex-none active:opacity-80"
          @click="download(d)"
        >
          <FileDown :size="17" class="text-primary-soft-fg" />
        </button>
      </div>
    </div>
  </div>
</template>
