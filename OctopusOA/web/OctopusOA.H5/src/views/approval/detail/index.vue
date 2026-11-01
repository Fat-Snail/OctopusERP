<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft, CheckCircle2, XCircle, Clock, Circle } from '@lucide/vue'
import {
  getApprovalById,
  approveApproval,
  rejectApproval,
  cancelApproval,
  getTemplateById,
} from '@/api/approval'
import { useUserStore } from '@/store/user'
import type { ApprovalResponse } from '@/api/approval/types'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const approvalId = Number(route.params['id'])
const approval = ref<ApprovalResponse | null>(null)
const loading = ref(true)
const formFields = ref<{ key: string; label: string; type: string; value?: string }[]>([])
const actionSheet = ref<'approve' | 'reject' | null>(null)
const comment = ref('')
const submitting = ref(false)
const errorMsg = ref('')

function statusLabel(s: string) {
  return { pending: '审批中', approved: '已通过', rejected: '已驳回', cancelled: '已撤回', draft: '草稿' }[s] ?? s
}

function statusClass(s: string) {
  return {
    pending: 'text-warning',
    approved: 'text-success',
    rejected: 'text-danger',
    cancelled: 'text-muted-foreground',
  }[s] ?? 'text-muted-foreground'
}

function nodeIcon(action: string) {
  if (action === 'approved') return CheckCircle2
  if (action === 'rejected') return XCircle
  return Circle
}

function nodeIconClass(action: string) {
  if (action === 'approved') return 'text-success'
  if (action === 'rejected') return 'text-danger'
  return 'text-muted-foreground'
}

function isMineToApprove() {
  if (!approval.value || approval.value.status !== 'pending') return false
  return !!userStore.sub
}

function isApplicant() {
  return approval.value?.applicantId?.toString() === userStore.sub
}

async function loadData() {
  loading.value = true
  try {
    const data = await getApprovalById(approvalId)
    approval.value = data
    try {
      const template = await getTemplateById(data.templateId)
      const schema = JSON.parse(template.formSchema) as { fields: { key: string; label: string; type: string }[] }
      const formData = JSON.parse(data.formData) as Record<string, unknown>
      formFields.value = schema.fields.map(f => ({
        ...f,
        value: String(formData[f.key] ?? '—'),
      }))
    } catch {
      formFields.value = []
    }
  } catch {
    errorMsg.value = '加载失败'
  } finally {
    loading.value = false
  }
}

async function handleApprove() {
  submitting.value = true
  try {
    await approveApproval(approvalId, comment.value)
    actionSheet.value = null
    await loadData()
  } catch (e: unknown) {
    errorMsg.value = (e as Error).message
  } finally {
    submitting.value = false
  }
}

async function handleReject() {
  submitting.value = true
  try {
    await rejectApproval(approvalId, comment.value)
    actionSheet.value = null
    await loadData()
  } catch (e: unknown) {
    errorMsg.value = (e as Error).message
  } finally {
    submitting.value = false
  }
}

async function handleCancel() {
  if (!confirm('确认撤回此申请？')) return
  try {
    await cancelApproval(approvalId)
    router.push('/approval')
  } catch (e: unknown) {
    errorMsg.value = (e as Error).message
  }
}

onMounted(loadData)
</script>

<template>
  <div class="min-h-full bg-subtle pb-24">
    <!-- Header -->
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4 gap-3">
        <button
          class="w-8 h-8 flex items-center justify-center rounded-full hover:bg-muted border-0 bg-transparent cursor-pointer"
          @click="router.back()"
        >
          <ArrowLeft :size="20" class="text-foreground" />
        </button>
        <h1 class="text-[16px] font-semibold text-foreground flex-1">审批详情</h1>
        <span v-if="approval" :class="['text-[13px] font-medium', statusClass(approval.status)]">
          {{ statusLabel(approval.status) }}
        </span>
      </div>
    </div>

    <div v-if="loading" class="py-16 text-center text-muted-foreground text-[12px]">加载中...</div>

    <div v-else-if="approval" class="p-4 space-y-3">
      <!-- Basic info -->
      <div class="bg-background border border-border rounded-xl p-4">
        <h2 class="text-[16px] font-semibold text-foreground mb-1">{{ approval.title }}</h2>
        <p class="text-[12px] text-muted-foreground">{{ approval.templateName }} · {{ approval.applicantName }}</p>
        <p class="text-[11px] text-muted-foreground mt-1">{{ approval.createTime }}</p>
        <div v-if="approval.isOverdue && approval.status === 'pending'" class="mt-2">
          <span class="text-[11px] font-medium px-2 py-0.5 rounded-full bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]">
            已超期
          </span>
        </div>
      </div>

      <!-- Form data -->
      <div v-if="formFields.length" class="bg-background border border-border rounded-xl divide-y divide-border overflow-hidden">
        <div v-for="f in formFields" :key="f.key" class="flex items-center px-4 py-3 gap-3">
          <span class="text-[12px] text-muted-foreground w-20 shrink-0">{{ f.label }}</span>
          <span class="text-[13px] text-foreground">{{ f.value }}</span>
        </div>
      </div>

      <!-- Workflow timeline -->
      <div class="bg-background border border-border rounded-xl p-4">
        <p class="text-[13px] font-medium text-foreground mb-4">审批流程</p>
        <div class="space-y-4">
          <!-- Apply step -->
          <div class="flex gap-3">
            <div class="flex flex-col items-center">
              <div class="w-6 h-6 rounded-full bg-primary-soft flex items-center justify-center shrink-0">
                <CheckCircle2 :size="14" class="text-[var(--primary-soft-fg)]" />
              </div>
              <div v-if="approval.records.length" class="w-[1px] flex-1 bg-border mt-1 min-h-[16px]" />
            </div>
            <div class="flex-1 pb-2">
              <p class="text-[13px] font-medium text-foreground">{{ approval.applicantName }} 发起申请</p>
              <p class="text-[11px] text-muted-foreground mt-0.5">{{ approval.createTime }}</p>
            </div>
          </div>

          <!-- Records -->
          <div v-for="(record, idx) in approval.records" :key="record.recordId" class="flex gap-3">
            <div class="flex flex-col items-center">
              <div class="w-6 h-6 rounded-full bg-muted flex items-center justify-center shrink-0">
                <component :is="nodeIcon(record.action)" :size="14" :class="nodeIconClass(record.action)" />
              </div>
              <div v-if="idx < approval.records.length - 1 || approval.status === 'pending'" class="w-[1px] flex-1 bg-border mt-1 min-h-[16px]" />
            </div>
            <div class="flex-1 pb-2">
              <p class="text-[13px] font-medium text-foreground">
                {{ record.approverName }} — {{ record.action === 'approved' ? '通过' : record.action === 'rejected' ? '驳回' : record.action }}
              </p>
              <p v-if="record.comment" class="text-[12px] text-muted-foreground mt-0.5">{{ record.comment }}</p>
              <p class="text-[11px] text-muted-foreground mt-0.5">{{ record.actionTime }}</p>
            </div>
          </div>

          <!-- Current pending node -->
          <div v-if="approval.status === 'pending'" class="flex gap-3">
            <div class="flex flex-col items-center">
              <div class="w-6 h-6 rounded-full border-2 border-warning flex items-center justify-center shrink-0">
                <Clock :size="12" class="text-warning" />
              </div>
            </div>
            <div class="flex-1">
              <p class="text-[13px] font-medium text-foreground">{{ approval.currentNodeName ?? '审批中' }}</p>
              <p class="text-[11px] text-muted-foreground mt-0.5">等待审批...</p>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Action buttons -->
    <div
      v-if="approval && !loading"
      class="fixed bottom-0 left-0 right-0 bg-background border-t border-border p-4 safe-area-inset-bottom"
    >
      <div v-if="isMineToApprove() && approval.status === 'pending'" class="flex gap-3">
        <button
          class="flex-1 h-12 text-danger text-[15px] font-medium rounded-xl hover:opacity-90 transition-opacity cursor-pointer border border-danger/30 bg-muted"
          @click="actionSheet = 'reject'"
        >
          驳 回
        </button>
        <button
          class="flex-1 h-12 text-white text-[15px] font-medium rounded-xl hover:opacity-90 transition-opacity cursor-pointer border-0 bg-[var(--success)]"
          @click="actionSheet = 'approve'"
        >
          通 过
        </button>
      </div>
      <button
        v-else-if="isApplicant() && approval.status === 'pending'"
        class="w-full h-12 bg-muted text-muted-foreground text-[15px] font-medium rounded-xl cursor-pointer border border-border"
        @click="handleCancel"
      >
        撤 回 申 请
      </button>
    </div>

    <!-- Action Sheet -->
    <Teleport to="body">
      <div v-if="actionSheet" class="fixed inset-0 z-50">
        <div class="absolute inset-0 bg-black/40" @click="actionSheet = null" />
        <div class="absolute bottom-0 left-0 right-0 bg-background rounded-t-2xl p-5 pb-8">
          <h3 class="text-[16px] font-semibold text-foreground mb-4">
            {{ actionSheet === 'approve' ? '确认通过' : '确认驳回' }}
          </h3>
          <textarea
            v-model="comment"
            :placeholder="actionSheet === 'approve' ? '审批意见（可选）' : '请填写驳回原因'"
            class="w-full h-24 px-3 py-2 rounded-[var(--radius)] border border-border bg-background text-[14px] text-foreground resize-none focus:outline-none focus:ring-2 focus:ring-primary mb-4"
          />
          <p v-if="errorMsg" class="text-[12px] text-danger mb-3">{{ errorMsg }}</p>
          <button
            :disabled="submitting"
            :class="[
              'w-full h-12 text-white text-[15px] font-medium rounded-xl hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border-0',
              actionSheet === 'approve' ? 'bg-[var(--success)]' : 'bg-[var(--danger)]'
            ]"
            @click="actionSheet === 'approve' ? handleApprove() : handleReject()"
          >
            {{ submitting ? '处理中...' : (actionSheet === 'approve' ? '确认通过' : '确认驳回') }}
          </button>
        </div>
      </div>
    </Teleport>
  </div>
</template>
