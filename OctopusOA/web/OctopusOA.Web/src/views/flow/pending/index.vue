<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Eye, Check, X } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'
import { toast } from '@/components/ui/toast'
import { getFlowPending, approveFlowTask, rejectFlowTask } from '@/api/flow'
import type { FlowTask } from '@/api/flow/types'

const router = useRouter()
const loading = ref(false)
const list = ref<FlowTask[]>([])
const actionVisible = ref(false)
const actionType = ref<'approve' | 'reject'>('approve')
const comment = ref('')
const submitting = ref(false)
const currentRow = ref<FlowTask | null>(null)

async function loadData() {
  loading.value = true
  try { list.value = (await getFlowPending()).rows } finally { loading.value = false }
}

function openDetail(row: FlowTask) {
  router.push(`/flow/detail/${row.instanceId}`)
}

function handleAction(row: FlowTask, type: 'approve' | 'reject') {
  currentRow.value = row
  actionType.value = type
  comment.value = ''
  actionVisible.value = true
}

async function submitAction() {
  if (!currentRow.value) return
  submitting.value = true
  try {
    if (actionType.value === 'approve') {
      await approveFlowTask(currentRow.value.taskId, comment.value || undefined)
      toast('审批通过', 'success')
    } else {
      await rejectFlowTask(currentRow.value.taskId, comment.value || undefined)
      toast('已驳回', 'info')
    }
    actionVisible.value = false
    loadData()
  } catch (e: unknown) {
    toast((e as Error).message, 'error')
  } finally {
    submitting.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader title="待我处理" sub="审批中心 · 需要你处理的任务" />
    <div class="p-5">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!list.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无待处理任务</div>
      <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">标题</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">申请人</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">当前节点</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-32">业务键</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">提交时间</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">操作</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in list" :key="row.taskId" class="hover:bg-subtle">
              <td class="px-3 py-2 font-medium truncate max-w-[220px]">{{ row.title }}</td>
              <td class="px-3 py-2">{{ row.applicantName }}</td>
              <td class="px-3 py-2"><Badge variant="warning">{{ row.nodeName }}</Badge></td>
              <td class="px-3 py-2 text-muted-foreground font-mono text-[11px]">{{ row.businessKey || '—' }}</td>
              <td class="px-3 py-2 text-muted-foreground whitespace-nowrap">{{ row.createTime }}</td>
              <td class="px-3 py-2 whitespace-nowrap">
                <div class="flex items-center gap-2">
                  <button class="flex items-center gap-0.5 text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent" @click="openDetail(row)">
                    <Eye :size="11" />详情
                  </button>
                  <button class="flex items-center gap-0.5 text-[11px] text-success hover:underline cursor-pointer border-0 bg-transparent" @click="handleAction(row, 'approve')">
                    <Check :size="11" />通过
                  </button>
                  <button class="flex items-center gap-0.5 text-[11px] text-danger hover:underline cursor-pointer border-0 bg-transparent" @click="handleAction(row, 'reject')">
                    <X :size="11" />驳回
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <Dialog v-model:open="actionVisible">
      <DialogContent class="p-5 w-[400px]">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">{{ actionType === 'approve' ? '审批通过' : '审批驳回' }}</DialogTitle>
        </DialogHeader>
        <label class="block text-[12px] text-foreground mb-1.5">审批意见（可选）</label>
        <textarea
          v-model="comment"
          rows="3"
          placeholder="请输入审批意见"
          class="w-full px-3 py-2 border border-input rounded-[var(--radius)] bg-card text-[12px] resize-none focus:outline-none focus:ring-2 focus:ring-ring"
        />
        <DialogFooter>
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="actionVisible = false">取消</button>
          <button
            :disabled="submitting"
            :class="['px-4 py-1.5 text-[12px] rounded-[var(--radius)] text-white cursor-pointer border-0 transition-colors disabled:opacity-50', actionType === 'approve' ? 'bg-success hover:brightness-105' : 'bg-danger hover:brightness-105']"
            @click="submitAction"
          >
            {{ submitting ? '提交中...' : actionType === 'approve' ? '确认通过' : '确认驳回' }}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
