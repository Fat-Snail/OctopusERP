<script setup lang="ts">
import { Check, X, Clock, MoreHorizontal, CheckCircle, XCircle, Flag, User } from 'lucide-vue-next'
import type { RecordResponse } from '@/api/approval/types'
import Badge from '@/components/ui/badge.vue'

interface NodeInfo { nodeName: string; nodeOrder: number }

const props = defineProps<{
  nodes: NodeInfo[]
  records: RecordResponse[]
  currentNodeOrder: number
  status: string
  applicantName: string
}>()

function getRecord(nodeOrder: number) {
  return props.records.find(r => r.nodeOrder === nodeOrder)
}

function getStatus(nodeOrder: number): 'approved' | 'rejected' | 'active' | 'waiting' {
  const record = getRecord(nodeOrder)
  if (record?.action === 'approve') return 'approved'
  if (record?.action === 'reject') return 'rejected'
  if (nodeOrder === props.currentNodeOrder && props.status === 'pending') return 'active'
  return 'waiting'
}

const endLabel = { approved: '已完成', rejected: '已驳回', cancelled: '已撤回', pending: '进行中', draft: '草稿' }
</script>

<template>
  <div class="flex items-start overflow-x-auto pb-2">
    <!-- Start node -->
    <div class="flex flex-col items-center min-w-[100px] px-2">
      <div class="w-10 h-10 rounded-full bg-primary-soft border-2 border-primary flex items-center justify-center">
        <User :size="18" class="text-primary" />
      </div>
      <div class="text-[12px] font-semibold mt-1">申请人</div>
      <div class="text-[11px] text-muted-foreground">{{ applicantName }}</div>
    </div>

    <!-- Nodes -->
    <template v-for="(node, idx) in nodes" :key="idx">
      <!-- Arrow -->
      <div class="flex items-center pt-5 shrink-0">
        <div class="w-8 h-0.5 bg-border-strong" />
        <div class="w-0 h-0 border-y-4 border-y-transparent border-l-[6px] border-l-border-strong" />
      </div>

      <div
        :class="[
          'flex flex-col items-center min-w-[110px] px-2 py-3 rounded-lg border-2 transition-all shrink-0',
          getStatus(idx + 1) === 'approved' ? 'border-success bg-success/10' :
          getStatus(idx + 1) === 'rejected' ? 'border-danger bg-danger/10' :
          getStatus(idx + 1) === 'active'   ? 'border-warning bg-warning/15 shadow-sm' :
          'border-border opacity-60'
        ]"
      >
        <div class="mb-1">
          <Check v-if="getStatus(idx + 1) === 'approved'" :size="20" class="text-success" />
          <X v-else-if="getStatus(idx + 1) === 'rejected'" :size="20" class="text-danger" />
          <Clock v-else-if="getStatus(idx + 1) === 'active'" :size="20" class="text-warning" />
          <MoreHorizontal v-else :size="20" class="text-muted-foreground" />
        </div>
        <div class="text-[12px] font-semibold text-center whitespace-nowrap">{{ node.nodeName }}</div>
        <div v-if="getRecord(idx + 1)" class="text-[11px] text-muted-foreground text-center mt-0.5">
          {{ getRecord(idx + 1)!.approverName }}
          <span v-if="getRecord(idx + 1)!.comment" class="block italic text-[10px]">
            「{{ getRecord(idx + 1)!.comment }}」
          </span>
        </div>
        <div v-else-if="getStatus(idx + 1) === 'active'" class="text-[11px] text-warning font-medium mt-0.5">待审批</div>
        <Badge
          :variant="getStatus(idx + 1) === 'approved' ? 'success' : getStatus(idx + 1) === 'rejected' ? 'danger' : getStatus(idx + 1) === 'active' ? 'warning' : 'secondary'"
          class="mt-1.5"
        >
          {{ { approved: '已通过', rejected: '已驳回', active: '审批中', waiting: '未到达' }[getStatus(idx + 1)] }}
        </Badge>
      </div>
    </template>

    <!-- End arrow -->
    <div class="flex items-center pt-5 shrink-0">
      <div class="w-8 h-0.5 bg-border-strong" />
      <div class="w-0 h-0 border-y-4 border-y-transparent border-l-[6px] border-l-border-strong" />
    </div>

    <!-- End node -->
    <div
      :class="[
        'flex flex-col items-center min-w-[100px] px-2 py-3 rounded-lg border-2 shrink-0',
        status === 'approved' ? 'border-success bg-success/10' :
        status === 'rejected' || status === 'cancelled' ? 'border-danger bg-danger/10' :
        'border-border bg-subtle'
      ]"
    >
      <CheckCircle v-if="status === 'approved'" :size="20" class="text-success mb-1" />
      <XCircle v-else-if="status === 'rejected' || status === 'cancelled'" :size="20" class="text-danger mb-1" />
      <Flag v-else :size="20" class="text-muted-foreground mb-1" />
      <div class="text-[12px] font-semibold">{{ endLabel[status as keyof typeof endLabel] || '结束' }}</div>
    </div>
  </div>
</template>
