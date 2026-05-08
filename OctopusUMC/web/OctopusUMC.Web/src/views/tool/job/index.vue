<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Search, RefreshCw, Plus, Edit, Trash2, Play, Pause, RotateCcw } from 'lucide-vue-next'
import { get, post, put, del } from '@/utils/http'
import type { JobResponse, JobLogResponse } from '@/api/system/types'
import type { PagedResult } from '@/api/types'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'

const loading = ref(false)
const submitting = ref(false)
const list = ref<JobResponse[]>([])
const total = ref(0)
const dialogOpen = ref(false)
const isEdit = ref(false)
const submitError = ref('')
const query = reactive({ pageNum: 1, pageSize: 10, jobName: '', status: '' })

// Log tab
const activeTab = ref<'jobs' | 'logs'>('jobs')
const logLoading = ref(false)
const logs = ref<JobLogResponse[]>([])
const logTotal = ref(0)
const logQuery = reactive({ pageNum: 1, pageSize: 20, status: '' })

interface JobForm {
  jobId?: number
  jobName: string
  jobGroup: string
  invokeTarget: string
  cronExpression: string
  status: 0 | 1
  remark: string
}

const formData = reactive<JobForm>({
  jobName: '', jobGroup: 'DEFAULT', invokeTarget: '', cronExpression: '', status: 1, remark: '',
})

async function loadData() {
  loading.value = true
  try {
    const data = await get<PagedResult<JobResponse>>('/monitor/job/list', query)
    list.value = data.rows
    total.value = data.total
  } finally { loading.value = false }
}

async function loadLogs() {
  logLoading.value = true
  try {
    const params: Record<string, unknown> = { pageNum: logQuery.pageNum, pageSize: logQuery.pageSize }
    if (logQuery.status !== '') params.status = logQuery.status
    const data = await get<PagedResult<JobLogResponse>>('/monitor/job/log/list', params)
    logs.value = data.rows
    logTotal.value = data.total
  } finally { logLoading.value = false }
}

function switchTab(tab: 'jobs' | 'logs') {
  activeTab.value = tab
  if (tab === 'logs' && logs.value.length === 0) loadLogs()
}

function resetQuery() { query.jobName = ''; query.status = ''; loadData() }

function openAdd() {
  isEdit.value = false
  Object.assign(formData, { jobName: '', jobGroup: 'DEFAULT', invokeTarget: '', cronExpression: '', status: 1, remark: '' })
  delete formData.jobId
  submitError.value = ''
  dialogOpen.value = true
}

function openEdit(row: JobResponse) {
  isEdit.value = true
  Object.assign(formData, {
    jobId: row.jobId, jobName: row.jobName, jobGroup: row.jobGroup,
    invokeTarget: row.invokeTarget, cronExpression: row.cronExpression,
    status: row.status, remark: row.remark ?? '',
  })
  submitError.value = ''
  dialogOpen.value = true
}

async function handleSubmit() {
  if (!formData.jobName.trim()) { submitError.value = '请输入任务名称'; return }
  if (!formData.invokeTarget.trim()) { submitError.value = '请输入调用目标'; return }
  if (!formData.cronExpression.trim()) { submitError.value = '请输入Cron表达式'; return }
  submitError.value = ''
  submitting.value = true
  try {
    if (isEdit.value) { await put('/monitor/job', formData) }
    else { await post('/monitor/job', formData) }
    dialogOpen.value = false
    loadData()
  } catch { submitError.value = '操作失败' } finally { submitting.value = false }
}

async function handleDelete(row: JobResponse) {
  if (!confirm(`确认删除任务 "${row.jobName}" 吗？`)) return
  await del(`/monitor/job/${row.jobId}`)
  loadData()
}

async function handleRun(row: JobResponse) {
  if (!confirm(`确认立即执行任务 "${row.jobName}" 吗？`)) return
  await put<null>(`/monitor/job/run/${row.jobId}`)
}

async function handlePause(row: JobResponse) {
  await put<null>(`/monitor/job/pause/${row.jobId}`)
  loadData()
}

async function handleResume(row: JobResponse) {
  await put<null>(`/monitor/job/resume/${row.jobId}`)
  loadData()
}

async function handleCleanLogs() {
  if (!confirm('确认清空所有任务日志吗？')) return
  await del('/monitor/job/log/clean')
  logs.value = []
  logTotal.value = 0
}

function formatMs(ms: number) {
  if (ms < 1000) return ms + 'ms'
  return (ms / 1000).toFixed(2) + 's'
}

onMounted(loadData)
</script>

<template>
  <div class="space-y-0">
    <!-- Tab bar -->
    <div class="flex border-b border-border bg-card rounded-t-lg px-4">
      <button v-for="tab in [{ key: 'jobs', label: '任务列表' }, { key: 'logs', label: '执行日志' }]"
        :key="tab.key"
        class="h-10 px-4 text-[13px] border-b-2 transition-colors cursor-pointer bg-transparent border-x-0 border-t-0"
        :class="activeTab === tab.key ? 'border-primary text-primary font-medium' : 'border-transparent text-muted-foreground hover:text-foreground'"
        @click="switchTab(tab.key as 'jobs' | 'logs')">
        {{ tab.label }}
      </button>
    </div>

    <!-- Job list tab -->
    <div v-if="activeTab === 'jobs'" class="bg-card border-x border-b border-border rounded-b-lg overflow-hidden">
      <div class="flex items-center gap-2 px-4 py-3 border-b border-border flex-wrap">
        <div class="flex items-center gap-1.5">
          <span class="text-[12px] text-muted-foreground">任务名称</span>
          <input v-model="query.jobName" placeholder="任务名称" class="h-7 w-[160px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div class="flex items-center gap-1.5">
          <span class="text-[12px] text-muted-foreground">状态</span>
          <select v-model="query.status" class="h-7 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
            <option value="">全部</option>
            <option value="1">正常</option>
            <option value="0">暂停</option>
          </select>
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
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">任务名称</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">任务组名</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">调用目标</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">Cron表达式</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-20">状态</th>
              <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loading"><td colspan="7" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="!list.length"><td colspan="7" class="py-8 text-center text-[12px] text-muted-foreground">暂无数据</td></tr>
            <tr v-else v-for="(row, idx) in list" :key="row.jobId" class="border-b border-border last:border-0 hover:bg-subtle">
              <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ (query.pageNum - 1) * query.pageSize + idx + 1 }}</td>
              <td class="px-4 py-2 text-[12px] text-foreground font-medium">{{ row.jobName }}</td>
              <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.jobGroup }}</td>
              <td class="px-4 py-2 text-[11px] font-mono-tnum text-muted-foreground truncate max-w-[200px]">{{ row.invokeTarget }}</td>
              <td class="px-4 py-2 text-[11px] font-mono-tnum text-foreground">{{ row.cronExpression }}</td>
              <td class="px-4 py-2"><Badge :variant="row.status === 1 ? 'success' : 'warning'">{{ row.status === 1 ? '正常' : '暂停' }}</Badge></td>
              <td class="px-4 py-2 text-right">
                <div class="flex items-center justify-end gap-1">
                  <button class="h-6 px-2 text-[11px] text-success hover:bg-success/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="handleRun(row)"><Play :size="10" /> 执行</button>
                  <button v-if="row.status === 1" class="h-6 px-2 text-[11px] text-warning hover:bg-warning/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="handlePause(row)"><Pause :size="10" /> 暂停</button>
                  <button v-else class="h-6 px-2 text-[11px] text-info hover:bg-info/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="handleResume(row)"><RotateCcw :size="10" /> 恢复</button>
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

    <!-- Log tab -->
    <div v-else class="bg-card border-x border-b border-border rounded-b-lg overflow-hidden">
      <div class="flex items-center gap-2 px-4 py-3 border-b border-border flex-wrap">
        <div class="flex items-center gap-1.5">
          <span class="text-[12px] text-muted-foreground">执行状态</span>
          <select v-model="logQuery.status" class="h-7 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
            <option value="">全部</option>
            <option value="0">成功</option>
            <option value="1">失败</option>
          </select>
        </div>
        <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="loadLogs"><Search :size="12" /> 搜索</button>
        <button class="h-7 px-3 bg-muted text-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-muted/80 cursor-pointer border border-border" @click="() => { logQuery.status = ''; loadLogs() }"><RefreshCw :size="12" /> 重置</button>
        <button class="h-7 px-3 bg-danger text-white text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0 ml-auto" @click="handleCleanLogs"><Trash2 :size="12" /> 清空日志</button>
      </div>
      <div class="overflow-x-auto">
        <table class="w-full">
          <thead>
            <tr class="bg-subtle border-b border-border">
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-14">序号</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">任务名称</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">调用目标</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">开始时间</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-24">耗时</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-20">状态</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">错误信息</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="logLoading"><td colspan="7" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="!logs.length"><td colspan="7" class="py-8 text-center text-[12px] text-muted-foreground">暂无日志</td></tr>
            <tr v-else v-for="(row, idx) in logs" :key="row.jobLogId" class="border-b border-border last:border-0 hover:bg-subtle">
              <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ (logQuery.pageNum - 1) * logQuery.pageSize + idx + 1 }}</td>
              <td class="px-4 py-2 text-[12px] text-foreground">{{ row.jobName }}</td>
              <td class="px-4 py-2 text-[11px] font-mono-tnum text-muted-foreground truncate max-w-[200px]">{{ row.invokeTarget }}</td>
              <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.startTime }}</td>
              <td class="px-4 py-2 text-[12px] font-mono-tnum text-foreground">{{ formatMs(row.elapsedMs) }}</td>
              <td class="px-4 py-2"><Badge :variant="row.status === 0 ? 'success' : 'danger'">{{ row.status === 0 ? '成功' : '失败' }}</Badge></td>
              <td class="px-4 py-2 text-[11px] text-danger truncate max-w-[240px]">{{ row.errorMsg ?? '-' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="flex items-center justify-between px-4 py-2.5 border-t border-border">
        <span class="text-[12px] text-muted-foreground">共 {{ logTotal }} 条</span>
        <div class="flex items-center gap-1">
          <button :disabled="logQuery.pageNum <= 1" class="h-7 px-2 text-[12px] text-foreground bg-muted rounded border border-border disabled:opacity-40 cursor-pointer hover:bg-muted/80" @click="() => { logQuery.pageNum--; loadLogs() }">上一页</button>
          <span class="h-7 px-3 flex items-center text-[12px] text-foreground">{{ logQuery.pageNum }}</span>
          <button :disabled="logQuery.pageNum * logQuery.pageSize >= logTotal" class="h-7 px-2 text-[12px] text-foreground bg-muted rounded border border-border disabled:opacity-40 cursor-pointer hover:bg-muted/80" @click="() => { logQuery.pageNum++; loadLogs() }">下一页</button>
        </div>
      </div>
    </div>
  </div>

  <Dialog v-model:open="dialogOpen">
    <DialogContent class="w-[560px]">
      <DialogHeader><DialogTitle>{{ isEdit ? '编辑任务' : '新增任务' }}</DialogTitle></DialogHeader>
      <div class="grid grid-cols-2 gap-3 py-2">
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">任务名称 <span class="text-danger">*</span></label>
          <input v-model="formData.jobName" placeholder="请输入任务名称" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">任务组名</label>
          <input v-model="formData.jobGroup" placeholder="DEFAULT" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div class="col-span-2">
          <label class="block text-[12px] font-medium text-foreground mb-1">调用目标 <span class="text-danger">*</span></label>
          <input v-model="formData.invokeTarget" placeholder="如：cleanOldLogs" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] font-mono-tnum focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">Cron表达式 <span class="text-danger">*</span></label>
          <input v-model="formData.cronExpression" placeholder="0 0 2 * * *" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] font-mono-tnum focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">状态</label>
          <div class="flex gap-4 mt-1.5">
            <label v-for="opt in [{ v: 1, l: '正常' }, { v: 0, l: '暂停' }]" :key="opt.v" class="flex items-center gap-1.5 cursor-pointer">
              <input v-model="formData.status" type="radio" :value="opt.v" class="w-3.5 h-3.5" /><span class="text-[12px]">{{ opt.l }}</span>
            </label>
          </div>
        </div>
        <div class="col-span-2">
          <label class="block text-[12px] font-medium text-foreground mb-1">备注</label>
          <input v-model="formData.remark" placeholder="备注" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
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
