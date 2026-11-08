<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ChevronLeft, CheckCircle2, XCircle, Clock, ArrowRight } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import DynamicForm from '@/views/approval/components/DynamicForm.vue'
import FlowGraphPreview from '../components/FlowGraphPreview.vue'
import { getFlowInstance, getFlowDefinition } from '@/api/flow'
import type { FlowInstance, FlowTaskDetail, FlowHistory } from '@/api/flow/types'
import type { FormExtension, FormSchema, FormField } from '@/api/approval/types'

const route = useRoute()
const router = useRouter()

const loading = ref(true)
const instance = ref<FlowInstance | null>(null)
const formFields = ref<FormField[]>([])
const formExtensions = ref<FormExtension[]>([])
const graphJson = ref('')
const formData = computed<Record<string, unknown>>(() => {
  if (!instance.value) return {}
  try { return JSON.parse(instance.value.formData) as Record<string, unknown> } catch { return {} }
})

const statusMeta: Record<string, { label: string; variant: 'warning' | 'success' | 'danger' | 'secondary' }> = {
  pending: { label: '审批中', variant: 'warning' },
  approved: { label: '已通过', variant: 'success' },
  rejected: { label: '已驳回', variant: 'danger' },
  cancelled: { label: '已撤回', variant: 'secondary' },
  draft: { label: '草稿', variant: 'secondary' },
}

const actionLabel: Record<string, string> = {
  approve: '通过', reject: '驳回', skip: '跳过', complete: '完成', error: '异常',
}

async function load() {
  loading.value = true
  try {
    const inst = await getFlowInstance(Number(route.params.id))
    instance.value = inst
    try {
      const def = await getFlowDefinition(inst.definitionId)
      graphJson.value = def.graphJson
      const parsed = JSON.parse(def.formSchema) as FormSchema
      formFields.value = parsed.fields ?? []
      formExtensions.value = parsed.extensions ?? []
    } catch { formFields.value = [] }
  } finally {
    loading.value = false
  }
}

function taskBadge(t: FlowTaskDetail): { label: string; variant: 'warning' | 'success' | 'danger' | 'secondary' } {
  if (t.status === 'approved') return { label: '已通过', variant: 'success' }
  if (t.status === 'rejected') return { label: '已驳回', variant: 'danger' }
  if (t.status === 'cancelled') return { label: '已取消', variant: 'secondary' }
  return { label: '待处理', variant: 'warning' }
}

function historyIcon(h: FlowHistory) {
  if (h.action === 'reject' || h.action === 'error') return XCircle
  if (h.action === 'complete' || h.action === 'approve') return CheckCircle2
  return Clock
}

onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="流程详情" sub="审批中心 · 流程跟踪" />
    <div class="p-5">
      <button class="flex items-center gap-1 text-[12px] text-muted-foreground hover:text-foreground mb-4 cursor-pointer border-0 bg-transparent" @click="router.back()">
        <ChevronLeft :size="14" />返回
      </button>

      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!instance" class="py-12 text-center text-muted-foreground text-[12px]">流程不存在</div>
      <div v-else class="space-y-5">
        <!-- 概要 -->
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-5">
          <div class="flex items-start justify-between gap-4">
            <div>
              <div class="text-[16px] font-semibold">{{ instance.title }}</div>
              <div class="text-[12px] text-muted-foreground mt-1">
                申请人 {{ instance.applicantName }} · {{ instance.createTime }}
                <span v-if="instance.businessKey"> · 业务键 <span class="font-mono">{{ instance.businessKey }}</span></span>
              </div>
            </div>
            <Badge :variant="statusMeta[instance.status]?.variant ?? 'secondary'">
              {{ statusMeta[instance.status]?.label ?? instance.status }}
            </Badge>
          </div>
        </div>

        <!-- 表单 -->
        <div v-if="formFields.length" class="bg-card border border-border rounded-[var(--radius-lg)] p-5">
          <div class="text-[13px] font-semibold mb-3">申请信息</div>
          <DynamicForm :fields="formFields" :extensions="formExtensions" :model-value="formData" readonly />
        </div>

        <!-- 任务 -->
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-5">
          <div class="text-[13px] font-semibold mb-3">审批任务</div>
          <div v-if="!instance.tasks.length" class="text-[12px] text-muted-foreground">暂无任务</div>
          <div v-else class="space-y-2">
            <div v-for="t in instance.tasks" :key="t.taskId" class="flex items-center gap-3 text-[12px]">
              <Badge :variant="taskBadge(t).variant">{{ taskBadge(t).label }}</Badge>
              <span class="font-medium">{{ t.nodeName }}</span>
              <span class="text-muted-foreground">{{ t.assigneeName }}</span>
              <span v-if="t.comment" class="text-muted-foreground italic">「{{ t.comment }}」</span>
              <span v-if="t.actionTime" class="ml-auto text-muted-foreground">{{ t.actionTime }}</span>
            </div>
          </div>
        </div>

        <!-- 流程跟踪图 -->
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-5">
          <div class="text-[13px] font-semibold mb-3">流程跟踪图</div>
          <FlowGraphPreview
            :graph-json="graphJson"
            :tasks="instance.tasks"
            :history="instance.history"
            :instance-status="instance.status"
          />
        </div>

        <!-- 流程跟踪时间线 -->
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-5">
          <div class="text-[13px] font-semibold mb-3">流程跟踪</div>
          <div v-if="!instance.history.length" class="text-[12px] text-muted-foreground">暂无流转记录</div>
          <div v-else class="relative pl-5">
            <div class="absolute left-[7px] top-1 bottom-1 w-px bg-border" />
            <div v-for="(h, idx) in instance.history" :key="idx" class="relative pb-4 last:pb-0">
              <component :is="historyIcon(h)" :size="14"
                :class="['absolute -left-5 top-0.5', h.action === 'reject' ? 'text-danger' : h.action === 'error' ? 'text-danger' : 'text-success']" />
              <div class="text-[12px]">
                <span class="font-medium">{{ h.nodeName || h.action }}</span>
                <span class="text-muted-foreground"> · {{ h.actorName }} </span>
                <span :class="['ml-1', h.action === 'reject' ? 'text-danger' : 'text-success']">{{ actionLabel[h.action] ?? h.action }}</span>
                <span v-if="h.comment" class="text-muted-foreground italic">（{{ h.comment }}）</span>
              </div>
              <div class="text-[11px] text-muted-foreground mt-0.5">{{ h.createTime }}</div>
            </div>
          </div>
        </div>

        <div v-if="instance.isOverdue" class="flex items-center gap-1 text-[12px] text-warning">
          <ArrowRight :size="12" />该流程已超期
        </div>
      </div>
    </div>
  </div>
</template>
