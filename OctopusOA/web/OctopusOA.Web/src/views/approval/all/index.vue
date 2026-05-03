<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Eye } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { getAllApprovals, getApprovalById, getTemplateById } from '@/api/approval'
import type { ApprovalResponse } from '@/api/approval/types'
import ApprovalDetail from '../components/ApprovalDetail.vue'

const loading = ref(false)
const list = ref<ApprovalResponse[]>([])
const statusFilter = ref('')
const drawerVisible = ref(false)
const currentApproval = ref<ApprovalResponse | null>(null)
const currentFormSchema = ref('')

const filteredList = computed(() =>
  statusFilter.value ? list.value.filter(a => a.status === statusFilter.value) : list.value
)

async function loadData() {
  loading.value = true
  try { list.value = (await getAllApprovals()).rows } finally { loading.value = false }
}

async function openDetail(row: ApprovalResponse) {
  currentApproval.value = await getApprovalById(row.approvalId)
  try { currentFormSchema.value = (await getTemplateById(row.templateId)).formSchema } catch { currentFormSchema.value = '' }
  drawerVisible.value = true
}

type BadgeVariant = 'warning' | 'success' | 'danger' | 'info' | 'secondary'
function statusVariant(s: string): BadgeVariant {
  return ({ pending: 'warning', approved: 'success', rejected: 'danger', cancelled: 'info' } as Record<string, BadgeVariant>)[s] ?? 'secondary'
}
function statusLabel(s: string) {
  return { pending: '审批中', approved: '已通过', rejected: '已驳回', cancelled: '已撤回' }[s] ?? s
}

const statusOptions = [
  { value: '', label: '全部' },
  { value: 'pending', label: '审批中' },
  { value: 'approved', label: '已通过' },
  { value: 'rejected', label: '已驳回' },
  { value: 'cancelled', label: '已撤回' },
]

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader title="全部审批" sub="查看系统中所有审批申请" />

    <div class="p-5">
      <!-- Filter -->
      <div class="flex items-center gap-2 mb-4">
        <span class="text-[12px] text-muted-foreground">状态：</span>
        <div class="flex gap-1">
          <button
            v-for="opt in statusOptions"
            :key="opt.value"
            :class="['px-3 py-1 text-[11px] rounded cursor-pointer border-0 transition-colors', statusFilter === opt.value ? 'bg-primary-soft text-primary-soft-fg font-medium' : 'text-muted-foreground hover:bg-muted']"
            @click="statusFilter = opt.value"
          >{{ opt.label }}</button>
        </div>
      </div>

      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!filteredList.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无审批记录</div>
      <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">标题</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">类型</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">申请人</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">状态</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">进度</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">提交时间</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">操作</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in filteredList" :key="row.approvalId" class="hover:bg-subtle">
              <td class="px-3 py-2 font-medium truncate max-w-[200px]">{{ row.title }}</td>
              <td class="px-3 py-2 text-muted-foreground">{{ row.templateName }}</td>
              <td class="px-3 py-2">{{ row.applicantName }}</td>
              <td class="px-3 py-2"><Badge :variant="statusVariant(row.status)">{{ statusLabel(row.status) }}</Badge></td>
              <td class="px-3 py-2 font-mono-tnum">{{ row.currentNodeOrder }}/{{ row.totalNodes }}</td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground whitespace-nowrap">{{ row.createTime }}</td>
              <td class="px-3 py-2 whitespace-nowrap">
                <button class="flex items-center gap-0.5 text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent" @click="openDetail(row)">
                  <Eye :size="11" />详情
                </button>
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
