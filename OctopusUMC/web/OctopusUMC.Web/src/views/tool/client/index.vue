<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Search, RefreshCw, Plus, Edit, Trash2 } from 'lucide-vue-next'
import { get, post, put, del } from '@/utils/http'
import type { OidcClientResponse, CreateOidcClientRequest, UpdateOidcClientRequest } from '@/api/system/types'
import type { PagedResult } from '@/api/types'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'

const loading = ref(false)
const submitting = ref(false)
const list = ref<OidcClientResponse[]>([])
const total = ref(0)
const dialogOpen = ref(false)
const isEdit = ref(false)
const submitError = ref('')
const query = reactive({ pageNum: 1, pageSize: 10, clientId: '' })

interface ClientForm {
  id?: string
  clientId: string
  clientName: string
  clientType: 'public' | 'confidential'
  redirectUris: string
  postLogoutRedirectUris: string
  status: 0 | 1
}

const formData = reactive<ClientForm>({
  clientId: '', clientName: '', clientType: 'public',
  redirectUris: '', postLogoutRedirectUris: '', status: 1,
})

async function loadData() {
  loading.value = true
  try {
    const data = await get<PagedResult<OidcClientResponse>>('/admin/clients', query)
    list.value = data.rows
    total.value = data.total
  } finally { loading.value = false }
}

function resetQuery() { query.clientId = ''; loadData() }

function openAdd() {
  isEdit.value = false
  Object.assign(formData, { clientId: '', clientName: '', clientType: 'public', redirectUris: '', postLogoutRedirectUris: '', status: 1 })
  delete formData.id
  submitError.value = ''
  dialogOpen.value = true
}

function openEdit(row: OidcClientResponse) {
  isEdit.value = true
  Object.assign(formData, {
    id: row.id, clientId: row.clientId, clientName: row.clientName,
    clientType: row.clientType,
    redirectUris: row.redirectUris.join('\n'),
    postLogoutRedirectUris: row.postLogoutRedirectUris.join('\n'),
    status: row.status,
  })
  submitError.value = ''
  dialogOpen.value = true
}

function parseUris(raw: string): string[] {
  return raw.split('\n').map(s => s.trim()).filter(Boolean)
}

async function handleSubmit() {
  if (!formData.clientId.trim()) { submitError.value = '请输入客户端ID'; return }
  if (!formData.clientName.trim()) { submitError.value = '请输入客户端名称'; return }
  submitError.value = ''
  submitting.value = true
  try {
    if (isEdit.value) {
      const req: UpdateOidcClientRequest = {
        id: formData.id!,
        clientId: formData.clientId, clientName: formData.clientName,
        clientType: formData.clientType,
        redirectUris: parseUris(formData.redirectUris),
        postLogoutRedirectUris: parseUris(formData.postLogoutRedirectUris),
        status: formData.status,
      }
      await put('/admin/clients', req)
    } else {
      const req: CreateOidcClientRequest = {
        clientId: formData.clientId, clientName: formData.clientName,
        clientType: formData.clientType,
        redirectUris: parseUris(formData.redirectUris),
        postLogoutRedirectUris: parseUris(formData.postLogoutRedirectUris),
        status: formData.status,
      }
      await post('/admin/clients', req)
    }
    dialogOpen.value = false
    loadData()
  } catch { submitError.value = '操作失败' } finally { submitting.value = false }
}

async function handleDelete(row: OidcClientResponse) {
  if (!confirm(`确认删除客户端 "${row.clientName}" 吗？`)) return
  await del(`/admin/clients/${row.id}`)
  loadData()
}

onMounted(loadData)
</script>

<template>
  <div class="bg-card border border-border rounded-lg overflow-hidden">
    <div class="flex items-center gap-2 px-4 py-3 border-b border-border flex-wrap">
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">客户端ID</span>
        <input v-model="query.clientId" placeholder="客户端ID" class="h-7 w-[180px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
      </div>
      <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="loadData"><Search :size="12" /> 搜索</button>
      <button class="h-7 px-3 bg-muted text-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-muted/80 cursor-pointer border border-border" @click="resetQuery"><RefreshCw :size="12" /> 重置</button>
    </div>
    <div class="px-4 py-2.5 border-b border-border">
      <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="openAdd"><Plus :size="12" /> 新增</button>
    </div>
    <div class="overflow-x-auto">
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-14">序号</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">客户端ID</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">客户端名称</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-28">客户端类型</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">回调URI</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-20">状态</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">创建时间</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="8" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td></tr>
          <tr v-else-if="!list.length"><td colspan="8" class="py-8 text-center text-[12px] text-muted-foreground">暂无数据</td></tr>
          <tr v-else v-for="(row, idx) in list" :key="row.id" class="border-b border-border last:border-0 hover:bg-subtle">
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ (query.pageNum - 1) * query.pageSize + idx + 1 }}</td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-foreground">{{ row.clientId }}</td>
            <td class="px-4 py-2 text-[12px] text-foreground font-medium">{{ row.clientName }}</td>
            <td class="px-4 py-2"><Badge :variant="row.clientType === 'public' ? 'info' : 'warning'">{{ row.clientType === 'public' ? '公开' : '机密' }}</Badge></td>
            <td class="px-4 py-2 text-[11px] font-mono-tnum text-muted-foreground truncate max-w-[200px]">{{ row.redirectUris.join(', ') }}</td>
            <td class="px-4 py-2"><Badge :variant="row.status === 1 ? 'success' : 'danger'">{{ row.status === 1 ? '正常' : '停用' }}</Badge></td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.createdAt }}</td>
            <td class="px-4 py-2 text-right">
              <div class="flex items-center justify-end gap-1">
                <button class="h-6 px-2 text-[11px] text-primary hover:bg-primary-soft rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="openEdit(row)"><Edit :size="10" /> 编辑</button>
                <button class="h-6 px-2 text-[11px] text-danger hover:bg-danger/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="handleDelete(row)"><Trash2 :size="10" /> 删除</button>
              </div>
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

  <Dialog v-model:open="dialogOpen">
    <DialogContent class="w-[600px]">
      <DialogHeader><DialogTitle>{{ isEdit ? '编辑客户端' : '新增客户端' }}</DialogTitle></DialogHeader>
      <div class="grid grid-cols-2 gap-3 py-2">
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">客户端ID <span class="text-danger">*</span></label>
          <input v-model="formData.clientId" :disabled="isEdit" placeholder="如：octopus-oa-web" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] font-mono-tnum focus:outline-none focus:ring-1 focus:ring-ring disabled:opacity-50" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">客户端名称 <span class="text-danger">*</span></label>
          <input v-model="formData.clientName" placeholder="如：OctopusOA Web" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">客户端类型</label>
          <select v-model="formData.clientType" class="w-full h-8 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
            <option value="public">公开（Public）</option>
            <option value="confidential">机密（Confidential）</option>
          </select>
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">状态</label>
          <div class="flex gap-4 mt-1.5">
            <label v-for="opt in [{ v: 1, l: '正常' }, { v: 0, l: '停用' }]" :key="opt.v" class="flex items-center gap-1.5 cursor-pointer">
              <input v-model="formData.status" type="radio" :value="opt.v" class="w-3.5 h-3.5" /><span class="text-[12px]">{{ opt.l }}</span>
            </label>
          </div>
        </div>
        <div class="col-span-2">
          <label class="block text-[12px] font-medium text-foreground mb-1">回调URI（每行一个）</label>
          <textarea v-model="formData.redirectUris" rows="3" placeholder="http://localhost:5174/callback" class="w-full px-2.5 py-2 rounded-[var(--radius)] border border-input bg-background text-[12px] font-mono-tnum focus:outline-none focus:ring-1 focus:ring-ring resize-y" />
        </div>
        <div class="col-span-2">
          <label class="block text-[12px] font-medium text-foreground mb-1">登出回调URI（每行一个）</label>
          <textarea v-model="formData.postLogoutRedirectUris" rows="2" placeholder="http://localhost:5174" class="w-full px-2.5 py-2 rounded-[var(--radius)] border border-input bg-background text-[12px] font-mono-tnum focus:outline-none focus:ring-1 focus:ring-ring resize-y" />
        </div>
      </div>
      <p v-if="submitError" class="text-[12px] text-danger">{{ submitError }}</p>
      <DialogFooter>
        <button class="h-8 px-4 bg-muted text-foreground text-[12px] rounded-[var(--radius)] hover:bg-muted/80 cursor-pointer border border-border" @click="dialogOpen = false">取消</button>
        <button :disabled="submitting" class="h-8 px-4 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] hover:opacity-90 disabled:opacity-50 cursor-pointer border-0" @click="handleSubmit">确定</button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
