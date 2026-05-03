<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Search, RefreshCw, Trash2, ExternalLink } from 'lucide-vue-next'
import { get, del } from '@/utils/http'
import type { FileResponse } from '@/api/system/types'
import type { PagedResult } from '@/api/types'
import Badge from '@/components/ui/badge.vue'

const loading = ref(false)
const list = ref<FileResponse[]>([])
const total = ref(0)
const selected = ref<number[]>([])
const query = reactive({ pageNum: 1, pageSize: 10, fileName: '', service: '' })

async function loadData() {
  loading.value = true
  try {
    const data = await get<PagedResult<FileResponse>>('/system/oss/list', query)
    list.value = data.rows
    total.value = data.total
  } finally { loading.value = false }
}

function resetQuery() { query.fileName = ''; query.service = ''; loadData() }

function toggleSelect(id: number) {
  const idx = selected.value.indexOf(id)
  if (idx >= 0) selected.value.splice(idx, 1)
  else selected.value.push(id)
}

function toggleAll() {
  if (selected.value.length === list.value.length) selected.value = []
  else selected.value = list.value.map(r => r.ossId)
}

async function handleDelete() {
  if (!selected.value.length) return
  if (!confirm(`确认删除选中的 ${selected.value.length} 个文件吗？`)) return
  await del(`/system/oss/${selected.value.join(',')}`)
  selected.value = []
  loadData()
}

function isImage(suffix: string) {
  return ['jpg', 'jpeg', 'png', 'gif', 'webp', 'svg'].includes(suffix.toLowerCase().replace('.', ''))
}

onMounted(loadData)
</script>

<template>
  <div class="bg-card border border-border rounded-lg overflow-hidden">
    <div class="flex items-center gap-2 px-4 py-3 border-b border-border flex-wrap">
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">文件名称</span>
        <input v-model="query.fileName" placeholder="文件名称" class="h-7 w-[160px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
      </div>
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">存储服务</span>
        <select v-model="query.service" class="h-7 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
          <option value="">全部</option>
          <option value="local">本地</option>
          <option value="oss">阿里云OSS</option>
          <option value="cos">腾讯COS</option>
        </select>
      </div>
      <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="loadData"><Search :size="12" /> 搜索</button>
      <button class="h-7 px-3 bg-muted text-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-muted/80 cursor-pointer border border-border" @click="resetQuery"><RefreshCw :size="12" /> 重置</button>
    </div>
    <div class="px-4 py-2.5 border-b border-border">
      <button :disabled="!selected.length" class="h-7 px-3 bg-danger text-white text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 disabled:opacity-40 cursor-pointer border-0" @click="handleDelete"><Trash2 :size="12" /> 删除</button>
    </div>
    <div class="overflow-x-auto">
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="px-4 py-2.5 w-10"><input type="checkbox" :checked="selected.length === list.length && list.length > 0" class="w-3.5 h-3.5 cursor-pointer" @change="toggleAll" /></th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-14">序号</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">文件名</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">原始名称</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-20">类型</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-20">存储服务</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">上传人</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">上传时间</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="9" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td></tr>
          <tr v-else-if="!list.length"><td colspan="9" class="py-8 text-center text-[12px] text-muted-foreground">暂无数据</td></tr>
          <tr v-else v-for="(row, idx) in list" :key="row.ossId" class="border-b border-border last:border-0 hover:bg-subtle">
            <td class="px-4 py-2"><input type="checkbox" :checked="selected.includes(row.ossId)" class="w-3.5 h-3.5 cursor-pointer" @change="toggleSelect(row.ossId)" /></td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ (query.pageNum - 1) * query.pageSize + idx + 1 }}</td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-foreground truncate max-w-[200px]">
              <div class="flex items-center gap-2">
                <img v-if="isImage(row.fileSuffix)" :src="row.url" class="w-6 h-6 rounded object-cover shrink-0" />
                <span v-else class="w-6 h-6 rounded bg-muted flex items-center justify-center text-[10px] font-mono-tnum text-muted-foreground shrink-0">{{ row.fileSuffix }}</span>
                <span class="truncate">{{ row.fileName }}</span>
              </div>
            </td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground truncate max-w-[160px]">{{ row.originalName }}</td>
            <td class="px-4 py-2"><Badge variant="secondary">{{ row.fileSuffix }}</Badge></td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.service }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.createBy }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.createTime }}</td>
            <td class="px-4 py-2 text-right">
              <a :href="row.url" target="_blank" rel="noopener" class="h-6 px-2 text-[11px] text-primary hover:bg-primary-soft rounded cursor-pointer inline-flex items-center gap-1"><ExternalLink :size="11" /> 预览</a>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div class="flex items-center justify-between px-4 py-2.5 border-t border-border">
      <span class="text-[12px] text-muted-foreground">共 {{ total }} 条</span>
      <div class="flex items-center gap-1">
        <button :disabled="query.pageNum <= 1" class="h-7 px-2 text-[12px] text-foreground bg-muted rounded border border-border disabled:opacity-40 cursor-pointer hover:bg-muted/80" @click="() => { query.pageNum--; loadData() }">上一页</button>
        <span class="h-7 px-3 flex items-center text-[12px] text-foreground">{{ query.pageNum }}</span>
        <button :disabled="query.pageNum * query.pageSize >= total" class="h-7 px-2 text-[12px] text-foreground bg-muted rounded border border-border disabled:opacity-40 cursor-pointer hover:bg-muted/80" @click="() => { query.pageNum++; loadData() }">下一页</button>
      </div>
    </div>
  </div>
</template>
