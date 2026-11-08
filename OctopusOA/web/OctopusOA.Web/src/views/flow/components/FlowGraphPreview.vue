<script setup lang="ts">
import { computed } from 'vue'
import { layoutGraph, edgePath, NODE_W, NODE_H } from '../flowLayout'
import type { FlowGraph, FlowHistory, FlowTaskDetail } from '@/api/flow/types'

const props = defineProps<{
  graphJson: string
  tasks: FlowTaskDetail[]
  history: FlowHistory[]
  instanceStatus: string
}>()

const graph = computed<FlowGraph>(() => {
  try { return JSON.parse(props.graphJson) as FlowGraph } catch { return { nodes: [], edges: [] } }
})

const layout = computed(() => layoutGraph(graph.value))

const layoutEdges = computed(() =>
  (graph.value.edges ?? [])
    .map(e => {
      const s = layout.value.nodes.find(n => n.id === e.from)
      const t = layout.value.nodes.find(n => n.id === e.to)
      return s && t ? { id: `${e.from}->${e.to}`, d: edgePath(s, t) } : null
    })
    .filter((x): x is { id: string; d: string } => x !== null)
)

type NodeStatus = 'approved' | 'rejected' | 'active' | 'waiting'

const statusById = computed(() => {
  const map = new Map<string, NodeStatus>()
  const histByNode = new Map<string, string[]>()
  for (const h of props.history) {
    const arr = histByNode.get(h.nodeName) ?? []
    arr.push(h.action)
    histByNode.set(h.nodeName, arr)
  }
  for (const n of layout.value.nodes) {
    const nodeTasks = props.tasks.filter(t => t.nodeId === n.id)
    const actions = histByNode.get(n.name) ?? []
    let status: NodeStatus = 'waiting'
    if (nodeTasks.some(t => t.status === 'rejected') || actions.includes('reject')) status = 'rejected'
    else if (nodeTasks.some(t => t.status === 'pending')) status = 'active'
    else if (nodeTasks.length && nodeTasks.every(t => t.status === 'approved')) status = 'approved'
    else if (actions.includes('approve') || actions.includes('complete')) status = 'approved'
    else if (n.type === 'start') status = 'approved'
    else if (n.type === 'end' && props.instanceStatus === 'approved') status = 'approved'
    map.set(n.id, status)
  }
  return map
})

const statusLabel: Record<NodeStatus, string> = {
  approved: '已通过', rejected: '已驳回', active: '待处理', waiting: '未到达',
}
const statusClass: Record<NodeStatus, string> = {
  approved: 'border-success bg-success/10',
  rejected: 'border-danger bg-danger/10',
  active: 'border-warning bg-warning/15 shadow-sm',
  waiting: 'border-border bg-card opacity-70',
}
const typeLabel: Record<string, string> = {
  start: '开始', end: '结束', approval: '审批', counter_sign: '会签', or_sign: '或签',
  exclusive_gateway: '条件分支', parallel_gateway: '并行网关', service_task: '服务任务',
  script: '脚本', sub_process: '子流程',
}
</script>

<template>
  <div>
    <div class="flex items-center gap-3 mb-2 text-[10.5px] text-muted-foreground">
      <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-success" />已通过</span>
      <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-warning" />待处理</span>
      <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-danger" />已驳回</span>
      <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-border" />未到达</span>
    </div>

    <div v-if="!layout.nodes.length" class="py-8 text-center text-[12px] text-muted-foreground">暂无流程图</div>
    <div v-else class="overflow-auto border border-border rounded-[var(--radius-lg)] bg-subtle/30 max-h-[420px]">
      <div class="relative" :style="{ width: layout.width + 'px', height: layout.height + 'px' }">
        <svg class="absolute inset-0 pointer-events-none" :width="layout.width" :height="layout.height">
          <defs>
            <marker id="flow-preview-arrow" markerWidth="10" markerHeight="10" refX="8" refY="3"
              orient="auto" markerUnits="strokeWidth">
              <path d="M0,0 L0,6 L9,3 z" fill="#94a3b8" />
            </marker>
          </defs>
          <path v-for="e in layoutEdges" :key="e.id" :d="e.d" fill="none"
            stroke="#94a3b8" stroke-width="1.5" marker-end="url(#flow-preview-arrow)" />
        </svg>

        <div
          v-for="n in layout.nodes"
          :key="n.id"
          :class="['absolute rounded-[var(--radius)] border-2 flex flex-col justify-center px-2', statusClass[statusById.get(n.id) ?? 'waiting']]"
          :style="{ left: n.x + 'px', top: n.y + 'px', width: NODE_W + 'px', height: NODE_H + 'px' }"
        >
          <div class="text-[10px] text-muted-foreground uppercase tracking-wide">{{ typeLabel[n.type] ?? n.type }}</div>
          <div class="text-[12px] font-semibold truncate">{{ n.name }}</div>
          <div class="text-[10px]">{{ statusLabel[statusById.get(n.id) ?? 'waiting'] }}</div>
        </div>
      </div>
    </div>
  </div>
</template>
