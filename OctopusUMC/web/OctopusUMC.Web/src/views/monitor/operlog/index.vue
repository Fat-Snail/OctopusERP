<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Search, RefreshCw, Trash2, Eye } from 'lucide-vue-next'
import { get, del } from '@/utils/http'
import type { OperlogResponse } from '@/api/monitor/types'
import type { PagedResult } from '@/api/types'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'

const loading = ref(false)
const list = ref<OperlogResponse[]>([])
const total = ref(0)
const selected = ref<number[]>([])
const detailOpen = ref(false)
const detail = ref<OperlogResponse | null>(null)
const query = reactive({ pageNum: 1, pageSize: 10, operName: '', title: '', status: '' })

async function loadData() {
  loading.value = true
  try {
    const data = await get<PagedResult<OperlogResponse>>('/monitor/operlog/list', query)
    list.value = data.rows
    total.value = data.total
  } finally { loading.value = false }
}

function resetQuery() { query.operName = ''; query.title = ''; query.status = ''; loadData() }

function toggleSelect(id: number) {
  const idx = selected.value.indexOf(id)
  if (idx >= 0) selected.value.splice(idx, 1)
  else selected.value.push(id)
}

function toggleAll() {
  if (selected.value.length === list.value.length) selected.value = []
  else selected.value = list.value.map(r => r.operId)
}

async function handleDelete() {
  if (!selected.value.length) return
  if (!confirm(`确认删除选中的 ${selected.value.length} 条操作日志吗？`)) return
  await del(`/monitor/operlog/${selected.value.join(',')}`)
  selected.value = []
  loadData()
}

async function handleClear() {
  if (!confirm('确认清空全部操作日志吗？此操作不可恢复。')) return
  await del('/monitor/operlog/clean')
  loadData()
}

function openDetail(row: OperlogResponse) {
  detail.value = row
  detailOpen.value = true
}

function statusVariant(status: number) {
  return status === 0 ? 'success' : 'danger'
}

onMounted(loadData)
</script>

<template>
  <div class="bg-card border border-border rounded-lg overflow-hidden">
    <div class="flex items-center gap-2 px-4 py-3 border-b border-border flex-wrap">
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">操作人员</span>
        <input v-model="query.operName" placeholder="操作人员" class="h-7 w-[120px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
      </div>
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">操作模块</span>
        <input v-model="query.title" placeholder="操作模块" class="h-7 w-[120px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
      </div>
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">操作状态</span>
        <select v-model="query.status" class="h-7 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
          <option value="">全部</option>
          <option value="0">正常</option>
          <option value="1">异常</option>
        </select>
      </div>
      <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="loadData"><Search :size="12" /> 搜索</button>
      <button class="h-7 px-3 bg-muted text-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-muted/80 cursor-pointer border border-border" @click="resetQuery"><RefreshCw :size="12" /> 重置</button>
    </div>
    <div class="px-4 py-2.5 border-b border-border flex items-center gap-2">
      <button :disabled="!selected.length" class="h-7 px-3 bg-danger text-white text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 disabled:opacity-40 cursor-pointer border-0" @click="handleDelete"><Trash2 :size="12" /> 删除</button>
      <button class="h-7 px-3 bg-muted text-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-muted/80 cursor-pointer border border-border" @click="handleClear"><Trash2 :size="12" /> 清空</button>
    </div>
    <div class="overflow-x-auto">
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="px-4 py-2.5 w-10"><input type="checkbox" :checked="selected.length === list.length && list.length > 0" class="w-3.5 h-3.5 cursor-pointer" @change="toggleAll" /></th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-14">序号</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作模块</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">请求方式</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作人员</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作地址</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-20">操作状态</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作时间</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">耗时(ms)</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="10" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td></tr>
          <tr v-else-if="!list.length"><td colspan="10" class="py-8 text-center text-[12px] text-muted-foreground">暂无数据</td></tr>
          <tr v-else v-for="(row, idx) in list" :key="row.operId" class="border-b border-border last:border-0 hover:bg-subtle">
            <td class="px-4 py-2"><input type="checkbox" :checked="selected.includes(row.operId)" class="w-3.5 h-3.5 cursor-pointer" @change="toggleSelect(row.operId)" /></td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ (query.pageNum - 1) * query.pageSize + idx + 1 }}</td>
            <td class="px-4 py-2 text-[12px] text-foreground">{{ row.title }}</td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ row.requestMethod }}</td>
            <td class="px-4 py-2 text-[12px] text-foreground">{{ row.operName }}</td>
            <td class="px-4 py-2 text-[11px] font-mono-tnum text-muted-foreground truncate max-w-[180px]">{{ row.operUrl }}</td>
            <td class="px-4 py-2"><Badge :variant="statusVariant(row.status)">{{ row.status === 0 ? '正常' : '异常' }}</Badge></td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.operTime }}</td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ row.costTime }}</td>
            <td class="px-4 py-2 text-right">
              <button class="h-6 px-2 text-[11px] text-primary hover:bg-primary-soft rounded cursor-pointer border-0 bg-transparent flex items-center gap-1 ml-auto" @click="openDetail(row)"><Eye :size="11" /> 详情</button>
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

  <Dialog v-model:open="detailOpen">
    <DialogContent class="w-[640px]">
      <DialogHeader><DialogTitle>操作日志详情</DialogTitle></DialogHeader>
      <div v-if="detail" class="space-y-2 py-2">
        <div class="grid grid-cols-2 gap-2 text-[12px]">
          <div><span class="text-muted-foreground">操作模块：</span><span class="text-foreground">{{ detail.title }}</span></div>
          <div><span class="text-muted-foreground">请求方式：</span><span class="font-mono-tnum text-foreground">{{ detail.requestMethod }}</span></div>
          <div><span class="text-muted-foreground">操作人员：</span><span class="text-foreground">{{ detail.operName }}</span></div>
          <div><span class="text-muted-foreground">操作地址：</span><span class="font-mono-tnum text-foreground">{{ detail.operUrl }}</span></div>
          <div class="col-span-2"><span class="text-muted-foreground">请求URL：</span><span class="font-mono-tnum text-foreground break-all">{{ detail.operUrl }}</span></div>
          <div><span class="text-muted-foreground">操作状态：</span><Badge :variant="statusVariant(detail.status)">{{ detail.status === 0 ? '正常' : '异常' }}</Badge></div>
          <div><span class="text-muted-foreground">耗时：</span><span class="font-mono-tnum text-foreground">{{ detail.costTime }} ms</span></div>
          <div><span class="text-muted-foreground">操作时间：</span><span class="text-foreground">{{ detail.operTime }}</span></div>
        </div>
        <div>
          <div class="text-[12px] text-muted-foreground mb-1">请求参数</div>
          <textarea readonly :value="detail.operParam" class="w-full h-20 px-2 py-1.5 rounded-[var(--radius)] border border-input bg-muted text-[11px] font-mono-tnum resize-none focus:outline-none" />
        </div>
        <div>
          <div class="text-[12px] text-muted-foreground mb-1">返回结果</div>
          <textarea readonly :value="detail.jsonResult" class="w-full h-20 px-2 py-1.5 rounded-[var(--radius)] border border-input bg-muted text-[11px] font-mono-tnum resize-none focus:outline-none" />
        </div>
        <div v-if="detail.errorMsg">
          <div class="text-[12px] text-danger mb-1">错误信息</div>
          <div class="px-2 py-1.5 rounded-[var(--radius)] bg-danger/5 border border-danger/20 text-[11px] font-mono-tnum text-danger break-all">{{ detail.errorMsg }}</div>
        </div>
      </div>
    </DialogContent>
  </Dialog>
</template>
