<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Search, RefreshCw, Trash2 } from 'lucide-vue-next'
import { get, del } from '@/utils/http'
import type { LoginfoResponse } from '@/api/monitor/types'
import type { PagedResult } from '@/api/types'
import Badge from '@/components/ui/badge.vue'

const loading = ref(false)
const list = ref<LoginfoResponse[]>([])
const total = ref(0)
const selected = ref<number[]>([])
const query = reactive({ pageNum: 1, pageSize: 10, userName: '', ipaddr: '', status: '' })

async function loadData() {
  loading.value = true
  try {
    const data = await get<PagedResult<LoginfoResponse>>('/monitor/logininfor/list', query)
    list.value = data.rows
    total.value = data.total
  } finally { loading.value = false }
}

function resetQuery() { query.userName = ''; query.ipaddr = ''; query.status = ''; loadData() }

function toggleSelect(id: number) {
  const idx = selected.value.indexOf(id)
  if (idx >= 0) selected.value.splice(idx, 1)
  else selected.value.push(id)
}

function toggleAll() {
  if (selected.value.length === list.value.length) selected.value = []
  else selected.value = list.value.map(r => r.infoId)
}

async function handleDelete() {
  if (!selected.value.length) return
  if (!confirm(`确认删除选中的 ${selected.value.length} 条登录日志吗？`)) return
  await del(`/monitor/logininfor/${selected.value.join(',')}`)
  selected.value = []
  loadData()
}

async function handleClear() {
  if (!confirm('确认清空全部登录日志吗？此操作不可恢复。')) return
  await del('/monitor/logininfor/clean')
  loadData()
}

onMounted(loadData)
</script>

<template>
  <div class="bg-card border border-border rounded-lg overflow-hidden">
    <div class="flex items-center gap-2 px-4 py-3 border-b border-border flex-wrap">
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">用户名</span>
        <input v-model="query.userName" placeholder="用户名" class="h-7 w-[120px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
      </div>
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">登录地址</span>
        <input v-model="query.ipaddr" placeholder="IP地址" class="h-7 w-[120px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
      </div>
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">登录状态</span>
        <select v-model="query.status" class="h-7 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
          <option value="">全部</option>
          <option value="0">成功</option>
          <option value="1">失败</option>
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
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">用户名</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">IP地址</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">登录地点</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">浏览器</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作系统</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-20">登录状态</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">提示信息</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">登录时间</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="10" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td></tr>
          <tr v-else-if="!list.length"><td colspan="10" class="py-8 text-center text-[12px] text-muted-foreground">暂无数据</td></tr>
          <tr v-else v-for="(row, idx) in list" :key="row.infoId" class="border-b border-border last:border-0 hover:bg-subtle">
            <td class="px-4 py-2"><input type="checkbox" :checked="selected.includes(row.infoId)" class="w-3.5 h-3.5 cursor-pointer" @change="toggleSelect(row.infoId)" /></td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ (query.pageNum - 1) * query.pageSize + idx + 1 }}</td>
            <td class="px-4 py-2 text-[12px] text-foreground font-medium">{{ row.userName }}</td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-foreground">{{ row.ipaddr }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.loginLocation }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.browser }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.os }}</td>
            <td class="px-4 py-2">
              <Badge :variant="row.status === 0 ? 'success' : 'danger'">{{ row.status === 0 ? '成功' : '失败' }}</Badge>
            </td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.msg }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.loginTime }}</td>
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
