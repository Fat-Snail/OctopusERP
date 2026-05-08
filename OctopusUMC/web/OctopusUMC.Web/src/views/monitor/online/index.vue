<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted } from 'vue'
import { Search, RefreshCw, UserX, Wifi } from 'lucide-vue-next'
import { del } from '@/utils/http'
import { startOnlineUserHub, stopOnlineUserHub } from '@/utils/hubService'
import type { OnlineUserResponse } from '@/api/monitor/types'
import type { PagedResult } from '@/api/types'
import * as signalR from '@microsoft/signalr'

const loading = ref(false)
const connected = ref(false)
const list = ref<OnlineUserResponse[]>([])
const total = ref(0)
const query = reactive({ pageNum: 1, pageSize: 10, userName: '', ipaddr: '' })

let hub: signalR.HubConnection | null = null

function applyFilter(rows: OnlineUserResponse[]) {
  return rows.filter(u =>
    (!query.userName || u.userName.includes(query.userName)) &&
    (!query.ipaddr || u.ipaddr.includes(query.ipaddr))
  )
}

async function loadData() {
  loading.value = true
  try {
    const res = await fetch(
      `${import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5001'}/api/monitor/online/list?pageNum=${query.pageNum}&pageSize=${query.pageSize}&userName=${encodeURIComponent(query.userName)}&ipaddr=${encodeURIComponent(query.ipaddr)}`,
      { credentials: 'include' }
    )
    const json = await res.json() as { code: number; data: PagedResult<OnlineUserResponse> }
    if (json.code === 200) {
      list.value = json.data.rows
      total.value = json.data.total
    }
  } finally {
    loading.value = false
  }
}

function resetQuery() { query.userName = ''; query.ipaddr = ''; loadData() }

async function handleForceLogout(row: OnlineUserResponse) {
  if (!confirm(`确认强制下线用户 "${row.userName}" 吗？`)) return
  await del(`/monitor/online/${row.tokenId}`)
  loadData()
}

async function initHub() {
  try {
    hub = await startOnlineUserHub()
    connected.value = true

    hub.on('UserConnected', () => loadData())
    hub.on('UserDisconnected', () => loadData())
    hub.onreconnected(() => { connected.value = true; loadData() })
    hub.onreconnecting(() => { connected.value = false })
    hub.onclose(() => { connected.value = false })
  } catch {
    connected.value = false
  }
}

onMounted(async () => {
  await loadData()
  await initHub()
})

onUnmounted(async () => {
  await stopOnlineUserHub()
})
</script>

<template>
  <div class="bg-card border border-border rounded-lg overflow-hidden">
    <div class="px-4 py-3 border-b border-border space-y-3">
      <div class="flex items-center gap-2">
        <Wifi :size="13" :class="connected ? 'text-[var(--success)]' : 'text-muted-foreground'" />
        <span class="text-[12px]" :class="connected ? 'text-[var(--success)]' : 'text-muted-foreground'">
          {{ connected ? 'SignalR 已连接，实时更新' : 'SignalR 未连接，手动刷新' }}
        </span>
      </div>
      <div class="flex items-center gap-2 flex-wrap">
        <div class="flex items-center gap-1.5">
          <span class="text-[12px] text-muted-foreground">用户名</span>
          <input v-model="query.userName" placeholder="用户名" class="h-7 w-[120px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div class="flex items-center gap-1.5">
          <span class="text-[12px] text-muted-foreground">登录地址</span>
          <input v-model="query.ipaddr" placeholder="IP地址" class="h-7 w-[120px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="loadData"><Search :size="12" /> 搜索</button>
        <button class="h-7 px-3 bg-muted text-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-muted/80 cursor-pointer border border-border" @click="resetQuery"><RefreshCw :size="12" /> 重置</button>
      </div>
    </div>
    <div class="overflow-x-auto">
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-14">序号</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">用户名</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">部门</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">IP地址</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">登录地点</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">浏览器</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作系统</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">登录时间</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="9" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td></tr>
          <tr v-else-if="!list.length"><td colspan="9" class="py-8 text-center text-[12px] text-muted-foreground">暂无在线用户</td></tr>
          <tr v-else v-for="(row, idx) in list" :key="row.tokenId" class="border-b border-border last:border-0 hover:bg-subtle">
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ (query.pageNum - 1) * query.pageSize + idx + 1 }}</td>
            <td class="px-4 py-2 text-[12px] text-foreground font-medium">{{ row.userName }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.deptName }}</td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-foreground">{{ row.ipaddr }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.loginLocation }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.browser }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.os }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.loginTime }}</td>
            <td class="px-4 py-2 text-right">
              <button class="h-6 px-2 text-[11px] text-danger hover:bg-danger/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1 ml-auto" @click="handleForceLogout(row)">
                <UserX :size="11" /> 强制下线
              </button>
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
