<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Eye, Undo2 } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { toast, confirm } from '@/components/ui/toast'
import { getMyApprovals, cancelApproval, getApprovalById, getTemplateById } from '@/api/approval'
import type { ApprovalResponse } from '@/api/approval/types'
import ApprovalDetail from '../components/ApprovalDetail.vue'

const loading = ref(false)
const list = ref<ApprovalResponse[]>([])
const drawerVisible = ref(false)
const currentApproval = ref<ApprovalResponse | null>(null)
const currentFormSchema = ref('')

async function loadData() {
  loading.value = true
  try { list.value = (await getMyApprovals()).rows } finally { loading.value = false }
}

async function openDetail(row: ApprovalResponse) {
  currentApproval.value = await getApprovalById(row.approvalId)
  try {
    const t = await getTemplateById(row.templateId)
    currentFormSchema.value = t.formSchema
  } catch { currentFormSchema.value = '' }
  drawerVisible.value = true
}

async function handleCancel(row: ApprovalResponse) {
  try {
    await confirm('确认撤回此申请？')
    await cancelApproval(row.approvalId)
    toast('已撤回', 'success')
    loadData()
  } catch { /* cancelled */ }
}

type StatusKey = 'pending' | 'approved' | 'rejected' | 'cancelled' | 'draft'
type BadgeVariant = 'warning' | 'success' | 'danger' | 'info' | 'secondary'
function statusVariant(s: string): BadgeVariant {
  return ({ pending: 'warning', approved: 'success', rejected: 'danger', cancelled: 'info', draft: 'secondary' } as Record<StatusKey, BadgeVariant>)[s as StatusKey] ?? 'secondary'
}
function statusLabel(s: string) {
  return { pending: '审批中', approved: '已通过', rejected: '已驳回', cancelled: '已撤回', draft: '草稿' }[s] ?? s
}

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader title="我的申请" sub="查看你发起的所有申请记录" />

    <div class="p-5">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!list.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无申请记录</div>
      <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">标题</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">类型</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">状态</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">进度</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">提交时间</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">操作</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in list" :key="row.approvalId" class="hover:bg-subtle transition-colors">
              <td class="px-3 py-2 font-medium truncate max-w-[240px]">{{ row.title }}</td>
              <td class="px-3 py-2 text-muted-foreground">{{ row.templateName }}</td>
              <td class="px-3 py-2">
                <Badge :variant="statusVariant(row.status)">{{ statusLabel(row.status) }}</Badge>
              </td>
              <td class="px-3 py-2 font-mono-tnum">{{ row.currentNodeOrder }}/{{ row.totalNodes }}</td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground whitespace-nowrap">{{ row.createTime }}</td>
              <td class="px-3 py-2 whitespace-nowrap">
                <div class="flex items-center gap-2">
                  <button
                    class="flex items-center gap-1 text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent"
                    @click="openDetail(row)"
                  >
                    <Eye :size="12" />详情
                  </button>
                  <button
                    v-if="row.status === 'pending'"
                    class="flex items-center gap-1 text-[11px] text-warning hover:underline cursor-pointer border-0 bg-transparent"
                    @click="handleCancel(row)"
                  >
                    <Undo2 :size="12" />撤回
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <Sheet v-model:open="drawerVisible">
      <SheetContent side="right" class="w-[600px]">
        <SheetHeader><SheetTitle>审批详情</SheetTitle></SheetHeader>
        <ApprovalDetail :approval="currentApproval" :form-schema="currentFormSchema" />
      </SheetContent>
    </Sheet>
  </div>
</template>
