<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Plus } from '@lucide/vue'
import {
  getMyApprovals,
  getPendingApprovals,
  getAvailableTemplates,
  submitApproval,
} from '@/api/approval'
import type { ApprovalResponse } from '@/api/approval/types'

const router = useRouter()
const route = useRoute()

type TabKey = 'pending' | 'mine' | 'apply'
const activeTab = ref<TabKey>((route.query['tab'] as TabKey) ?? 'pending')
const loading = ref(false)
const pendingList = ref<ApprovalResponse[]>([])
const mineList = ref<ApprovalResponse[]>([])

watch(() => route.query['tab'], (v) => {
  if (v) activeTab.value = v as TabKey
})

async function loadData() {
  loading.value = true
  try {
    if (activeTab.value === 'pending') {
      pendingList.value = (await getPendingApprovals()).rows
    } else if (activeTab.value === 'mine') {
      mineList.value = (await getMyApprovals()).rows
    }
  } finally {
    loading.value = false
  }
}

watch(activeTab, loadData)
onMounted(loadData)

function statusLabel(s: string) {
  return { pending: '审批中', approved: '已通过', rejected: '已驳回', cancelled: '已撤回', draft: '草稿' }[s] ?? s
}

function statusClass(s: string) {
  return {
    pending: 'bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)]',
    approved: 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]',
    rejected: 'bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]',
    cancelled: 'bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]',
  }[s] ?? 'bg-muted text-muted-foreground'
}

const displayList = () => activeTab.value === 'pending' ? pendingList.value : mineList.value

// Apply tab
const templates = ref<{ templateId: number; templateName: string; templateCode: string; description: string | null; icon: string | null }[]>([])
onMounted(async () => { templates.value = await getAvailableTemplates() })

// Quick apply modal
const applyModal = ref(false)
const selectedTemplate = ref<number | null>(null)
const applyTitle = ref('')
const applyData = ref('{}')
const submitting = ref(false)
const applyError = ref('')

async function handleApply() {
  if (!selectedTemplate.value || !applyTitle.value) { applyError.value = '请选择模板并填写标题'; return }
  submitting.value = true
  applyError.value = ''
  try {
    await submitApproval({ templateId: selectedTemplate.value, title: applyTitle.value, formData: applyData.value })
    applyModal.value = false
    applyTitle.value = ''
    selectedTemplate.value = null
    activeTab.value = 'mine'
    await loadData()
  } catch (e: unknown) {
    applyError.value = (e as Error).message
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="min-h-full">
    <!-- Header -->
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4">
        <h1 class="text-[17px] font-semibold text-foreground flex-1">审批</h1>
        <button
          class="flex items-center gap-1 text-primary text-[13px] border-0 bg-transparent cursor-pointer"
          @click="applyModal = true"
        >
          <Plus :size="16" /> 发起
        </button>
      </div>

      <!-- Tabs -->
      <div class="flex border-b border-border px-4">
        <button
          v-for="tab in [{ key: 'pending', label: '待审批' }, { key: 'mine', label: '我的申请' }] as const"
          :key="tab.key"
          :class="[
            'flex-1 py-2.5 text-[13px] font-medium border-b-2 -mb-px transition-colors cursor-pointer border-0 bg-transparent',
            activeTab === tab.key ? 'border-primary text-primary' : 'border-transparent text-muted-foreground'
          ]"
          @click="activeTab = tab.key"
        >
          {{ tab.label }}
        </button>
      </div>
    </div>

    <!-- List -->
    <div class="p-4">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!displayList().length" class="py-12 text-center text-muted-foreground text-[12px]">暂无记录</div>
      <div v-else class="space-y-3">
        <div
          v-for="item in displayList()"
          :key="item.approvalId"
          class="bg-background border border-border rounded-xl p-4 cursor-pointer hover:bg-muted/30 transition-colors"
          @click="router.push(`/h5/approval/detail/${item.approvalId}`)"
        >
          <div class="flex items-start justify-between gap-2 mb-2">
            <p class="text-[14px] font-medium text-foreground flex-1 leading-snug">{{ item.title }}</p>
            <div class="flex items-center gap-1 shrink-0">
              <span v-if="item.isOverdue && item.status === 'pending'" class="text-[10px] font-medium px-1.5 py-0.5 rounded-full bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)]">超期</span>
              <span :class="['text-[11px] font-medium px-2 py-0.5 rounded-full', statusClass(item.status)]">
                {{ statusLabel(item.status) }}
              </span>
            </div>
          </div>
          <p class="text-[12px] text-muted-foreground">{{ item.templateName }}</p>
          <div class="flex items-center justify-between mt-2">
            <p class="text-[11px] text-muted-foreground">{{ item.applicantName }}</p>
            <p class="text-[11px] text-muted-foreground">{{ item.createTime }}</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Apply modal -->
    <Teleport to="body">
      <div v-if="applyModal" class="fixed inset-0 z-50">
        <div class="absolute inset-0 bg-black/40" @click="applyModal = false" />
        <div class="absolute bottom-0 left-0 right-0 bg-background rounded-t-2xl p-5 pb-8 max-h-[80vh] overflow-y-auto">
          <h3 class="text-[16px] font-semibold text-foreground mb-4">发起申请</h3>

          <!-- Template grid -->
          <p class="text-[12px] text-muted-foreground mb-2">选择申请类型</p>
          <div class="grid grid-cols-3 gap-2 mb-4">
            <button
              v-for="t in templates"
              :key="t.templateId"
              :class="[
                'flex flex-col items-center gap-1.5 py-3 rounded-xl border text-[12px] cursor-pointer transition-colors',
                selectedTemplate === t.templateId
                  ? 'border-primary bg-primary-soft text-[var(--primary-soft-fg)]'
                  : 'border-border bg-background text-foreground'
              ]"
              @click="selectedTemplate = t.templateId"
            >
              <span class="text-[20px]">{{ t.icon || '📋' }}</span>
              <span>{{ t.templateName }}</span>
            </button>
          </div>

          <!-- Title input -->
          <p class="text-[12px] text-muted-foreground mb-2">申请标题</p>
          <input
            v-model="applyTitle"
            placeholder="请输入申请标题"
            class="w-full h-11 px-3 rounded-[var(--radius)] border border-input bg-background text-[14px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring mb-4"
          />

          <p v-if="applyError" class="text-[12px] text-danger mb-3">{{ applyError }}</p>
          <button
            :disabled="submitting"
            class="w-full h-12 bg-primary text-primary-foreground text-[15px] font-medium rounded-[var(--radius)] hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border-0"
            @click="handleApply"
          >
            {{ submitting ? '提交中...' : '确认提交' }}
          </button>
        </div>
      </div>
    </Teleport>
  </div>
</template>
