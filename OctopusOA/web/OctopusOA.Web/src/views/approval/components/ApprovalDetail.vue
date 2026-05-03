<script setup lang="ts">
import { computed } from 'vue'
import type { ApprovalResponse, FormSchema } from '@/api/approval/types'
import Badge from '@/components/ui/badge.vue'
import DynamicForm from './DynamicForm.vue'
import WorkflowGraph from './WorkflowGraph.vue'

const props = defineProps<{ approval: ApprovalResponse | null; formSchema?: string }>()

const formFields = computed(() => {
  if (!props.formSchema) return []
  try {
    const s = JSON.parse(props.formSchema) as FormSchema
    return s.fields || []
  } catch { return [] }
})

const parsedFormData = computed({
  get: () => {
    try { return JSON.parse(props.approval?.formData || '{}') as Record<string, unknown> }
    catch { return {} }
  },
  set: () => { /* readonly */ },
})

const workflowNodes = computed(() => {
  if (!props.approval) return []
  return Array.from({ length: props.approval.totalNodes }, (_, i) => {
    const record = props.approval!.records.find(r => r.nodeOrder === i + 1)
    return { nodeName: record?.nodeName || `节点 ${i + 1}`, nodeOrder: i + 1 }
  })
})

type BadgeVariant = 'warning' | 'success' | 'danger' | 'info' | 'secondary'
function statusVariant(status: string): BadgeVariant {
  return ({ pending: 'warning', approved: 'success', rejected: 'danger', cancelled: 'info', draft: 'secondary' } as Record<string, BadgeVariant>)[status] ?? 'secondary'
}
function statusLabel(status: string) {
  return { pending: '审批中', approved: '已通过', rejected: '已驳回', cancelled: '已撤回', draft: '草稿' }[status] ?? status
}
</script>

<template>
  <div v-if="approval" class="space-y-4">
    <!-- Basic info grid -->
    <div class="grid grid-cols-2 gap-px bg-border rounded-[var(--radius)] overflow-hidden border border-border text-[12px]">
      <div class="bg-card px-3 py-2 flex gap-2">
        <span class="text-muted-foreground w-16 shrink-0">标题</span>
        <span class="font-medium">{{ approval.title }}</span>
      </div>
      <div class="bg-card px-3 py-2 flex gap-2">
        <span class="text-muted-foreground w-16 shrink-0">模板</span>
        <span>{{ approval.templateName }}</span>
      </div>
      <div class="bg-card px-3 py-2 flex gap-2">
        <span class="text-muted-foreground w-16 shrink-0">申请人</span>
        <span>{{ approval.applicantName }}</span>
      </div>
      <div class="bg-card px-3 py-2 flex items-center gap-2">
        <span class="text-muted-foreground w-16 shrink-0">状态</span>
        <Badge :variant="statusVariant(approval.status)">{{ statusLabel(approval.status) }}</Badge>
      </div>
      <div class="bg-card px-3 py-2 flex gap-2">
        <span class="text-muted-foreground w-16 shrink-0">提交时间</span>
        <span class="font-mono-tnum whitespace-nowrap">{{ approval.createTime }}</span>
      </div>
      <div class="bg-card px-3 py-2 flex gap-2">
        <span class="text-muted-foreground w-16 shrink-0">更新时间</span>
        <span class="font-mono-tnum whitespace-nowrap">{{ approval.updateTime }}</span>
      </div>
    </div>

    <!-- Form data -->
    <div>
      <h4 class="text-[12px] font-semibold text-foreground mb-2">申请内容</h4>
      <DynamicForm v-if="formFields.length" :fields="formFields" v-model="parsedFormData" :readonly="true" />
      <div v-else class="text-[12px] text-muted-foreground py-2">无表单数据</div>
    </div>

    <!-- Workflow graph -->
    <div>
      <h4 class="text-[12px] font-semibold text-foreground mb-2">审批流程</h4>
      <WorkflowGraph
        :nodes="workflowNodes"
        :records="approval.records"
        :current-node-order="approval.currentNodeOrder"
        :status="approval.status"
        :applicant-name="approval.applicantName"
      />
    </div>

    <!-- Records table -->
    <div v-if="approval.records.length">
      <h4 class="text-[12px] font-semibold text-foreground mb-2">操作记录</h4>
      <div class="border border-border rounded-[var(--radius)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-32">节点</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">审批人</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">操作</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">意见</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">时间</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="r in approval.records" :key="r.recordId" class="hover:bg-subtle">
              <td class="px-3 py-2">{{ r.nodeName }}</td>
              <td class="px-3 py-2">{{ r.approverName }}</td>
              <td class="px-3 py-2">
                <Badge :variant="r.action === 'approve' ? 'success' : 'danger'">
                  {{ r.action === 'approve' ? '通过' : '驳回' }}
                </Badge>
              </td>
              <td class="px-3 py-2 text-muted-foreground truncate max-w-[200px]">{{ r.comment }}</td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground whitespace-nowrap">{{ r.actionTime }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
