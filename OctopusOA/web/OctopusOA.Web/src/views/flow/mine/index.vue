<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Eye } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { getMyFlows } from '@/api/flow'
import type { FlowInstanceSummary } from '@/api/flow/types'

const router = useRouter()
const loading = ref(false)
const rows = ref<FlowInstanceSummary[]>([])

const statusMeta: Record<string, { label: string; variant: 'warning' | 'success' | 'danger' | 'secondary' }> = {
  pending: { label: '审批中', variant: 'warning' },
  approved: { label: '已通过', variant: 'success' },
  rejected: { label: '已驳回', variant: 'danger' },
  cancelled: { label: '已撤回', variant: 'secondary' },
  draft: { label: '草稿', variant: 'secondary' },
}

async function loadData() {
  loading.value = true
  try { rows.value = (await getMyFlows()).rows } finally { loading.value = false }
}

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader title="我的申请" sub="我发起的审批流程" />
    <div class="p-5">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!rows.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无申请</div>
      <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">标题</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">状态</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-32">业务键</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">提交时间</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">操作</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in rows" :key="row.id" class="hover:bg-subtle">
              <td class="px-3 py-2 font-medium truncate max-w-[260px]">{{ row.title }}</td>
              <td class="px-3 py-2">
                <Badge :variant="statusMeta[row.status]?.variant ?? 'secondary'">{{ statusMeta[row.status]?.label ?? row.status }}</Badge>
              </td>
              <td class="px-3 py-2 text-muted-foreground font-mono text-[11px]">{{ row.businessKey || '—' }}</td>
              <td class="px-3 py-2 text-muted-foreground whitespace-nowrap">{{ row.createTime }}</td>
              <td class="px-3 py-2">
                <button class="flex items-center gap-0.5 text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent" @click="router.push(`/flow/detail/${row.id}`)">
                  <Eye :size="11" />详情
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
