<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Eye, Check, X, MoreHorizontal, Forward, UserPlus, Undo2 } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'
import { toast } from '@/components/ui/toast'
import {
  getFlowPending, approveFlowTask, rejectFlowTask,
  delegateFlowTask, addSignFlowTask, getRejectableNodes, rejectFlowTaskTo,
} from '@/api/flow'
import type { FlowTask } from '@/api/flow/types'

type ActionType = 'approve' | 'reject' | 'delegate' | 'add_sign' | 'reject_to'

const router = useRouter()
const loading = ref(false)
const list = ref<FlowTask[]>([])
const actionVisible = ref(false)
const actionType = ref<ActionType>('approve')
const comment = ref('')
const submitting = ref(false)
const currentRow = ref<FlowTask | null>(null)
const openMenuId = ref<number | null>(null)

// 委托/加签目标用户；驳回目标节点
const toUserId = ref('')
const toUserName = ref('')
const targetNodeId = ref('')
const rejectableNodes = ref<{ nodeId: string; nodeName: string }[]>([])

async function loadData() {
  loading.value = true
  try { list.value = (await getFlowPending()).rows } finally { loading.value = false }
}

function openDetail(row: FlowTask) {
  router.push(`/flow/detail/${row.instanceId}`)
}

async function handleAction(row: FlowTask, type: ActionType) {
  currentRow.value = row
  actionType.value = type
  comment.value = ''
  toUserId.value = ''
  toUserName.value = ''
  targetNodeId.value = ''
  openMenuId.value = null
  if (type === 'reject_to') {
    try { rejectableNodes.value = await getRejectableNodes(row.taskId) }
    catch { rejectableNodes.value = [] }
  }
  actionVisible.value = true
}

const actionTitles: Record<ActionType, string> = {
  approve: '审批通过', reject: '审批驳回', delegate: '委托/转交任务',
  add_sign: '加签协办人', reject_to: '驳回到指定节点',
}

async function submitAction() {
  if (!currentRow.value) return
  if ((actionType.value === 'delegate' || actionType.value === 'add_sign') && !toUserId.value) {
    toast('请填写目标用户的 UMC ID', 'warning'); return
  }
  if (actionType.value === 'reject_to' && !targetNodeId.value) {
    toast('请选择要驳回到的节点', 'warning'); return
  }
  submitting.value = true
  try {
    const id = currentRow.value.taskId
    switch (actionType.value) {
      case 'approve':
        await approveFlowTask(id, comment.value || undefined); toast('审批通过', 'success'); break
      case 'reject':
        await rejectFlowTask(id, comment.value || undefined); toast('已驳回', 'info'); break
      case 'delegate':
        await delegateFlowTask(id, Number(toUserId.value), toUserName.value || toUserId.value, comment.value || undefined)
        toast('已转交', 'success'); break
      case 'add_sign':
        await addSignFlowTask(id, Number(toUserId.value), toUserName.value || toUserId.value, comment.value || undefined)
        toast('已加签', 'success'); break
      case 'reject_to':
        await rejectFlowTaskTo(id, targetNodeId.value, comment.value || undefined)
        toast('已驳回至指定节点', 'info'); break
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
                  <div class="relative">
                    <button class="flex items-center gap-0.5 text-[11px] text-muted-foreground hover:text-foreground cursor-pointer border-0 bg-transparent" @click="openMenuId = openMenuId === row.taskId ? null : row.taskId">
                      <MoreHorizontal :size="13" />
                    </button>
                    <div v-if="openMenuId === row.taskId" class="absolute right-0 top-6 z-20 w-[132px] bg-card border border-border rounded-[var(--radius)] shadow-lg py-1">
                      <button class="w-full text-left px-2.5 py-1.5 text-[11px] hover:bg-subtle flex items-center gap-1.5 cursor-pointer border-0 bg-transparent" @click="handleAction(row, 'delegate')">
                        <Forward :size="11" />委托/转交
                      </button>
                      <button class="w-full text-left px-2.5 py-1.5 text-[11px] hover:bg-subtle flex items-center gap-1.5 cursor-pointer border-0 bg-transparent" @click="handleAction(row, 'add_sign')">
                        <UserPlus :size="11" />加签协办
                      </button>
                      <button class="w-full text-left px-2.5 py-1.5 text-[11px] hover:bg-subtle flex items-center gap-1.5 cursor-pointer border-0 bg-transparent" @click="handleAction(row, 'reject_to')">
                        <Undo2 :size="11" />驳回至节点
                      </button>
                    </div>
                  </div>
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
          <DialogTitle class="text-[14px] font-semibold">{{ actionTitles[actionType] }}</DialogTitle>
        </DialogHeader>

        <!-- 委托/加签：目标用户 -->
        <template v-if="actionType === 'delegate' || actionType === 'add_sign'">
          <label class="block text-[12px] text-foreground mb-1.5">目标用户 UMC ID <span class="text-danger">*</span></label>
          <input v-model="toUserId" type="number" placeholder="如 3（李四）"
            class="w-full h-[30px] px-3 mb-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          <label class="block text-[12px] text-foreground mb-1.5">显示名称（可选，留空用 ID）</label>
          <input v-model="toUserName" placeholder="如 李四"
            class="w-full h-[30px] px-3 mb-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
        </template>

        <!-- 驳回至节点：目标节点 -->
        <template v-else-if="actionType === 'reject_to'">
          <label class="block text-[12px] text-foreground mb-1.5">驳回到的节点 <span class="text-danger">*</span></label>
          <select v-model="targetNodeId" class="w-full h-[30px] px-2 mb-2 border border-input rounded-[var(--radius)] bg-card text-[12px]">
            <option value="" disabled>请选择节点</option>
            <option v-for="n in rejectableNodes" :key="n.nodeId" :value="n.nodeId">{{ n.nodeName }} ({{ n.nodeId }})</option>
          </select>
          <p v-if="!rejectableNodes.length" class="text-[11px] text-muted-foreground mb-2">当前节点之前没有可驳回的审批节点</p>
        </template>

        <label class="block text-[12px] text-foreground mb-1.5">{{ actionType === 'approve' || actionType === 'reject' ? '审批意见（可选）' : '说明（可选）' }}</label>
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
            :class="['px-4 py-1.5 text-[12px] rounded-[var(--radius)] text-white cursor-pointer border-0 transition-colors disabled:opacity-50', actionType === 'approve' || actionType === 'delegate' || actionType === 'add_sign' ? 'bg-success hover:brightness-105' : 'bg-danger hover:brightness-105']"
            @click="submitAction"
          >
            {{ submitting ? '提交中...' : actionType === 'approve' ? '确认通过' : actionType === 'reject' ? '确认驳回' : '确认提交' }}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
