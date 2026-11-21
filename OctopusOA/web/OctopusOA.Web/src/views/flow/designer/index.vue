<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Save, Trash2, Link2, MousePointer2, Plus } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import { toast } from '@/components/ui/toast'
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import FormDesigner from '../components/FormDesigner.vue'
import ConditionBuilder from '../components/ConditionBuilder.vue'
import { getFlowDefinition, saveFlowDefinition } from '@/api/flow'
import { layoutGraph, edgePath as buildEdgePath } from '../flowLayout'
import type { FlowGraph, FlowGraphEdge, FlowGraphNode, FlowNodeConfig } from '@/api/flow/types'
import type { FormField, FormSchema } from '@/api/approval/types'
import { FLOW_TEMPLATE_DRAFT_KEY, type FlowTemplateDraft } from '../flowTemplates'

interface DesignNode extends FlowGraphNode {
  x: number
  y: number
}

const route = useRoute()
const router = useRouter()

const NODE_W = 150
const NODE_H = 54
const LAYER_GAP = 92
const NODE_GAP = 44
const PADDING = 60
const canvasW = ref(1600)
const canvasH = ref(1000)

const defId = computed(() => {
  const id = route.params.id
  const str = Array.isArray(id) ? id[0] : id
  return str && str !== 'new' ? Number(str) : 0
})

const loading = ref(false)
const saving = ref(false)
const name = ref('')
const code = ref('')
const category = ref('')
const businessKey = ref('')
const formSchema = ref('{"fields":[]}')
const formDesignerOpen = ref(false)
const parsedFormFields = computed<FormField[]>(() => {
  try {
    return (JSON.parse(formSchema.value) as FormSchema).fields ?? []
  } catch {
    return []
  }
})
const formFieldCount = computed(() => parsedFormFields.value.length)

const nodes = ref<DesignNode[]>([])
const edges = ref<FlowGraphEdge[]>([])
const selectedNodeId = ref<string | null>(null)
const selectedEdgeIndex = ref<number | null>(null)
const connectMode = ref(false)
const connectFrom = ref<string | null>(null)

const canvasRef = ref<HTMLDivElement | null>(null)
let seq = 0

// ── 节点类型元数据 ──────────────────────────────────
const typeMeta: Record<string, { label: string; border: string; bg: string; text: string }> = {
  start: { label: '开始', border: 'border-success', bg: 'bg-success/10', text: 'text-success' },
  end: { label: '结束', border: 'border-danger', bg: 'bg-danger/10', text: 'text-danger' },
  approval: { label: '审批', border: 'border-primary', bg: 'bg-primary-soft', text: 'text-primary' },
  counter_sign: { label: '会签', border: 'border-info', bg: 'bg-info/10', text: 'text-info' },
  or_sign: { label: '或签', border: 'border-info', bg: 'bg-info/10', text: 'text-info' },
  exclusive_gateway: { label: '条件分支', border: 'border-warning', bg: 'bg-warning/10', text: 'text-warning' },
  parallel_gateway: { label: '并行网关', border: 'border-warning', bg: 'bg-warning/10', text: 'text-warning' },
  service_task: { label: '服务任务', border: 'border-success', bg: 'bg-success/10', text: 'text-success' },
}
const palette = Object.entries(typeMeta).map(([type, m]) => ({ type, ...m }))

function meta(type: string) {
  return typeMeta[type] ?? { label: type, border: 'border-border', bg: 'bg-card', text: 'text-foreground' }
}
function isApproverNode(type: string) {
  return type === 'approval' || type === 'counter_sign' || type === 'or_sign'
}

// ── 节点增删 ────────────────────────────────────────
function nextId(prefix: string) {
  seq += 1
  return `${prefix}_${Date.now().toString(36)}_${seq}`
}
function addNodeAt(type: string, x: number, y: number) {
  const node: DesignNode = {
    id: nextId(type),
    type,
    name: meta(type).label,
    x: Math.max(0, x),
    y: Math.max(0, y),
    config: isApproverNode(type) ? { approverType: 'role', approverValue: 'oa_admin', slaHours: 24 } : undefined,
  }
  nodes.value.push(node)
  ensureCanvas(node.x, node.y)
  selectNode(node.id)
}
function addNode(type: string) {
  const count = nodes.value.length
  addNodeAt(type, 80 + (count % 5) * 200, 120 + Math.floor(count / 5) * 130)
}

function removeSelectedNode() {
  if (!selectedNodeId.value) return
  const id = selectedNodeId.value
  nodes.value = nodes.value.filter(n => n.id !== id)
  edges.value = edges.value.filter(e => e.from !== id && e.to !== id)
  selectedNodeId.value = null
}
function removeSelectedEdge() {
  if (selectedEdgeIndex.value == null) return
  edges.value.splice(selectedEdgeIndex.value, 1)
  selectedEdgeIndex.value = null
}

// ── 连线 ────────────────────────────────────────────
function selectNode(id: string) {
  if (connectMode.value) {
    if (!connectFrom.value) { connectFrom.value = id; return }
    if (connectFrom.value === id) { connectFrom.value = null; return }
    if (!edges.value.some(e => e.from === connectFrom.value && e.to === id)) {
      edges.value.push({ from: connectFrom.value, to: id, condition: null })
    }
    connectFrom.value = null
    return
  }
  selectedNodeId.value = id
  selectedEdgeIndex.value = null
}
function selectEdge(i: number) {
  selectedNodeId.value = null
  selectedEdgeIndex.value = i
}
function toggleConnect() {
  connectMode.value = !connectMode.value
  connectFrom.value = null
  if (connectMode.value) { selectedNodeId.value = null; selectedEdgeIndex.value = null }
}

// ── 拖拽 ────────────────────────────────────────────
const dragging = ref<{ id: string; offsetX: number; offsetY: number } | null>(null)

function onNodePointerDown(e: PointerEvent, node: DesignNode) {
  if (connectMode.value) return
  e.preventDefault()
  const rect = canvasRef.value?.getBoundingClientRect()
  if (!rect) return
  dragging.value = {
    id: node.id,
    offsetX: e.clientX - rect.left - node.x,
    offsetY: e.clientY - rect.top - node.y,
  }
  selectedNodeId.value = node.id
  selectedEdgeIndex.value = null
}
function onPointerMove(e: PointerEvent) {
  if (!dragging.value || !canvasRef.value) return
  const node = nodes.value.find(n => n.id === dragging.value!.id)
  if (!node) return
  const rect = canvasRef.value.getBoundingClientRect()
  node.x = Math.max(0, e.clientX - rect.left - dragging.value.offsetX)
  node.y = Math.max(0, e.clientY - rect.top - dragging.value.offsetY)
  ensureCanvas(node.x, node.y)
}
function onPointerUp() { dragging.value = null }

// ── 调色板拖放 ──────────────────────────────────────
function onPaletteDragStart(e: DragEvent, type: string) {
  e.dataTransfer?.setData('application/flow-node', type)
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'copy'
}
function onCanvasDrop(e: DragEvent) {
  e.preventDefault()
  const type = e.dataTransfer?.getData('application/flow-node')
  if (!type || !canvasRef.value) return
  const rect = canvasRef.value.getBoundingClientRect()
  addNodeAt(type, e.clientX - rect.left - NODE_W / 2, e.clientY - rect.top - NODE_H / 2)
}

// ── 选中态同步到编辑表单 ────────────────────────────
const selectedNode = computed(() => nodes.value.find(n => n.id === selectedNodeId.value) ?? null)
const selectedEdge = computed(() => selectedEdgeIndex.value != null ? edges.value[selectedEdgeIndex.value] ?? null : null)

const editName = ref('')
const editApproverType = ref('role')
const editApproverValue = ref('')
const editSlaHours = ref('')
const editServiceKind = ref('')
const editCondition = ref('')

watch(selectedNodeId, (id) => {
  const n = nodes.value.find(x => x.id === id)
  if (!n) return
  editName.value = n.name
  editApproverType.value = n.config?.approverType ?? 'role'
  editApproverValue.value = n.config?.approverValue ?? ''
  editSlaHours.value = n.config?.slaHours != null ? String(n.config.slaHours) : ''
  editServiceKind.value = n.config?.serviceKind ?? ''
})
watch(selectedEdgeIndex, () => {
  editCondition.value = selectedEdge.value?.condition ?? ''
})

function applyToNode() {
  const n = selectedNode.value
  if (!n) return
  n.name = editName.value || meta(n.type).label
  if (isApproverNode(n.type)) {
    const cfg: FlowNodeConfig = {
      ...(n.config ?? {}),
      approverType: editApproverType.value,
      approverValue: editApproverValue.value || null,
      slaHours: editSlaHours.value ? Number(editSlaHours.value) : null,
    }
    n.config = cfg
  } else if (n.type === 'service_task') {
    n.config = { ...(n.config ?? {}), serviceKind: editServiceKind.value || null }
  }
}
function applyToEdge() {
  const e = selectedEdge.value
  if (e) e.condition = editCondition.value || null
}

// ── 连线几何 ────────────────────────────────────────
function edgePoints(e: FlowGraphEdge) {
  const s = nodes.value.find(n => n.id === e.from)
  const t = nodes.value.find(n => n.id === e.to)
  if (!s || !t) return null
  return { sx: s.x + NODE_W / 2, sy: s.y + NODE_H, tx: t.x + NODE_W / 2, ty: t.y }
}
function edgePath(e: FlowGraphEdge) {
  const s = nodes.value.find(n => n.id === e.from)
  const t = nodes.value.find(n => n.id === e.to)
  if (!s || !t) return ''
  return buildEdgePath(s, t)
}
function edgeLabelPos(e: FlowGraphEdge) {
  const p = edgePoints(e)
  if (!p) return { x: 0, y: 0 }
  return { x: (p.sx + p.tx) / 2, y: (p.sy + p.ty) / 2 - 6 }
}

function ensureCanvas(x: number, y: number) {
  canvasW.value = Math.max(canvasW.value, Math.ceil(x + NODE_W + PADDING))
  canvasH.value = Math.max(canvasH.value, Math.ceil(y + NODE_H + PADDING))
}

/// 分层自动布局：按拓扑深度自上而下分层，同层节点横向均分；避免所有节点挤在一条直线上。
function autoLayout() {
  const result = layoutGraph({ nodes: nodes.value, edges: edges.value })
  const byId = new Map(result.nodes.map(n => [n.id, n]))
  for (const n of nodes.value) {
    const p = byId.get(n.id)
    if (p) { n.x = p.x; n.y = p.y }
  }
  canvasW.value = result.width
  canvasH.value = result.height
}

// ── 加载 / 保存 ─────────────────────────────────────
function initNew() {
  // 模板草稿：从模板库新建时载入（读完即消费，避免污染后续新建）
  const raw = sessionStorage.getItem(FLOW_TEMPLATE_DRAFT_KEY)
  if (raw) {
    try {
      const draft = JSON.parse(raw) as FlowTemplateDraft
      const g = JSON.parse(draft.graphJson) as FlowGraph
      nodes.value = g.nodes.map(n => ({ ...n, x: n.x ?? 300, y: n.y ?? 120 }))
      edges.value = g.edges.map(e => ({ ...e }))
      name.value = draft.name
      code.value = draft.code
      category.value = draft.category ?? ''
      formSchema.value = draft.formSchema
      sessionStorage.removeItem(FLOW_TEMPLATE_DRAFT_KEY)
      return
    } catch {
      sessionStorage.removeItem(FLOW_TEMPLATE_DRAFT_KEY)
    }
  }
  nodes.value = [
    { id: 'start', type: 'start', name: '开始', x: 300, y: 80 },
    { id: 'end', type: 'end', name: '结束', x: 300, y: 320 },
  ]
  edges.value = []
}

async function load() {
  if (!defId.value) { initNew(); return }
  loading.value = true
  try {
    const d = await getFlowDefinition(defId.value)
    name.value = d.name
    code.value = d.code
    category.value = d.category ?? ''
    businessKey.value = d.businessKey ?? ''
    formSchema.value = d.formSchema
    const graph = JSON.parse(d.graphJson) as FlowGraph
    const needLayout = (graph.nodes ?? []).some(n => n.x == null || n.y == null)
    nodes.value = (graph.nodes ?? []).map(n => ({
      id: n.id,
      type: n.type,
      name: n.name,
      x: n.x ?? 0,
      y: n.y ?? 0,
      config: n.config ?? undefined,
    }))
    edges.value = graph.edges ?? []
    if (needLayout) autoLayout()
  } catch (e: unknown) {
    toast((e as Error).message, 'error')
  } finally {
    loading.value = false
  }
}

async function save() {
  if (!name.value.trim()) { toast('请输入流程名称', 'warning'); return }
  if (!code.value.trim()) { toast('请输入流程编码', 'warning'); return }
  if (!nodes.value.length) { toast('请先添加节点', 'warning'); return }
  if (!nodes.value.some(n => n.type === 'start')) { toast('必须包含开始节点', 'warning'); return }

  const graph: FlowGraph = {
    nodes: nodes.value.map(n => ({ id: n.id, type: n.type, name: n.name, x: n.x, y: n.y, config: n.config })),
    edges: edges.value.map(e => ({ from: e.from, to: e.to, condition: e.condition || null })),
  }
  saving.value = true
  try {
    const res = await saveFlowDefinition({
      id: defId.value || undefined,
      code: code.value.trim(),
      name: name.value.trim(),
      category: category.value.trim() || undefined,
      businessKey: businessKey.value.trim() || undefined,
      formSchema: formSchema.value,
      graphJson: JSON.stringify(graph),
    })
    toast('保存成功', 'success')
    if (!defId.value) router.replace(`/flow/designer/${res.id}`)
  } catch (e: unknown) {
    toast((e as Error).message, 'error')
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  load()
  window.addEventListener('pointermove', onPointerMove)
  window.addEventListener('pointerup', onPointerUp)
})
onBeforeUnmount(() => {
  window.removeEventListener('pointermove', onPointerMove)
  window.removeEventListener('pointerup', onPointerUp)
})
</script>

<template>
  <div>
    <PageHeader :title="defId ? '流程设计' : '新建流程'" sub="拖拽节点建模 · 连线 · 配置审批人">
      <template #actions>
        <button
          :disabled="saving"
          class="flex items-center gap-1 px-3 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50"
          @click="save"
        >
          <Save :size="13" />{{ saving ? '保存中...' : '保存' }}
        </button>
      </template>
    </PageHeader>

    <div v-if="loading" class="py-16 text-center text-muted-foreground text-[12px]">加载中...</div>
    <div v-else class="px-4 pb-4">
      <!-- 元信息 -->
      <div class="flex flex-wrap items-center gap-2 mb-3">
        <input v-model="name" placeholder="流程名称" class="h-[30px] px-2.5 w-[180px] border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        <input v-model="code" placeholder="编码，如 leave" class="h-[30px] px-2.5 w-[160px] border border-input rounded-[var(--radius)] bg-card text-[12px] font-mono focus:outline-none focus:ring-1 focus:ring-ring" />
        <input v-model="category" placeholder="分类（可选）" class="h-[30px] px-2.5 w-[130px] border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        <input v-model="businessKey" placeholder="业务键（可选）" class="h-[30px] px-2.5 w-[150px] border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        <button
          :class="['flex items-center gap-1 px-2.5 h-[30px] text-[12px] rounded-[var(--radius)] border cursor-pointer', connectMode ? 'bg-primary text-primary-foreground border-primary' : 'bg-card border-border hover:bg-muted']"
          @click="toggleConnect"
        >
          <Link2 :size="12" />{{ connectMode ? (connectFrom ? '选择目标节点...' : '依次点击两节点连线') : '连线模式' }}
        </button>
        <button
          class="flex items-center gap-1 px-2.5 h-[30px] text-[12px] rounded-[var(--radius)] border border-border bg-card hover:bg-muted cursor-pointer"
          @click="autoLayout"
        >
          自动布局
        </button>
        <button
          v-if="selectedNode"
          class="flex items-center gap-1 px-2.5 h-[30px] text-[12px] rounded-[var(--radius)] border border-danger text-danger bg-card hover:bg-danger/10 cursor-pointer"
          @click="removeSelectedNode"
        >
          <Trash2 :size="12" />删除节点
        </button>
        <button
          v-if="selectedEdge"
          class="flex items-center gap-1 px-2.5 h-[30px] text-[12px] rounded-[var(--radius)] border border-danger text-danger bg-card hover:bg-danger/10 cursor-pointer"
          @click="removeSelectedEdge"
        >
          <Trash2 :size="12" />删除连线
        </button>
      </div>

      <div class="flex gap-3" style="height: calc(100vh - 260px); min-height: 460px">
        <!-- 调色板 -->
        <div class="w-[132px] shrink-0 bg-card border border-border rounded-[var(--radius-lg)] p-2 overflow-y-auto">
          <div class="text-[10px] uppercase tracking-wide text-muted-foreground px-1 py-1.5">节点</div>
          <button
            v-for="p in palette"
            :key="p.type"
            draggable="true"
            :class="['w-full flex items-center gap-1.5 px-2 py-1.5 mb-1 rounded-[var(--radius)] border text-[11.5px] cursor-grab active:cursor-grabbing hover:brightness-105 text-left', p.bg, p.border, p.text]"
            @dragstart="onPaletteDragStart($event, p.type)"
            @click="addNode(p.type)"
          >
            <Plus :size="11" />{{ p.label }}
          </button>
          <div class="mt-2 text-[10px] text-muted-foreground leading-relaxed px-1">
            拖到画布，或点击添加。节点可拖动，连线模式依次点两节点。
          </div>
        </div>

        <!-- 画布 -->
        <div class="flex-1 overflow-auto bg-subtle/40 border border-border rounded-[var(--radius-lg)]">
          <div
            ref="canvasRef"
            class="relative"
            :style="{ width: canvasW + 'px', height: canvasH + 'px' }"
            @dragover.prevent
            @drop="onCanvasDrop"
            @click="selectedNodeId = null; selectedEdgeIndex = null"
          >
            <svg class="absolute inset-0 pointer-events-none" :width="canvasW" :height="canvasH">
              <defs>
                <marker id="flow-arrow" markerWidth="10" markerHeight="10" refX="8" refY="3" orient="auto" markerUnits="strokeWidth">
                  <path d="M0,0 L0,6 L9,3 z" fill="#94a3b8" />
                </marker>
              </defs>
              <g v-for="(e, i) in edges" :key="i">
                <path :d="edgePath(e)" fill="none" stroke="transparent" stroke-width="16"
                  class="pointer-events-auto cursor-pointer" @click.stop="selectEdge(i)" />
                <path :d="edgePath(e)" fill="none"
                  :stroke="selectedEdgeIndex === i ? 'var(--primary)' : '#94a3b8'"
                  :stroke-width="selectedEdgeIndex === i ? 2.5 : 1.5"
                  marker-end="url(#flow-arrow)" />
                <text v-if="e.condition" :x="edgeLabelPos(e).x" :y="edgeLabelPos(e).y"
                  text-anchor="middle" class="pointer-events-none" style="font-size: 10px; fill: #64748b">
                  {{ e.condition }}
                </text>
              </g>
            </svg>

            <div
              v-for="n in nodes"
              :key="n.id"
              :class="[
                'absolute select-none rounded-[var(--radius)] border-2 flex flex-col justify-center px-2 cursor-move transition-shadow',
                meta(n.type).bg, meta(n.type).border,
                selectedNodeId === n.id ? 'ring-2 ring-primary/40 shadow-md' : 'hover:shadow-sm',
                connectFrom === n.id ? 'ring-2 ring-warning' : '',
              ]"
              :style="{ left: n.x + 'px', top: n.y + 'px', width: NODE_W + 'px', height: NODE_H + 'px' }"
              @pointerdown="onNodePointerDown($event, n)"
              @click.stop="selectNode(n.id)"
            >
              <div :class="['text-[10px] font-medium uppercase tracking-wide', meta(n.type).text]">{{ meta(n.type).label }}</div>
              <div class="text-[12px] font-semibold truncate">{{ n.name }}</div>
            </div>
          </div>
        </div>

        <!-- 配置面板 -->
        <div class="w-[250px] shrink-0 bg-card border border-border rounded-[var(--radius-lg)] p-3 overflow-y-auto">
          <template v-if="selectedNode">
            <div class="text-[12px] font-semibold mb-2">节点配置</div>
            <label class="block text-[11px] text-muted-foreground mb-0.5">名称</label>
            <input v-model="editName" @input="applyToNode"
              class="w-full h-[28px] px-2 mb-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />

            <template v-if="isApproverNode(selectedNode.type)">
              <label class="block text-[11px] text-muted-foreground mb-0.5">审批人策略</label>
              <select v-model="editApproverType" @change="applyToNode"
                class="w-full h-[28px] px-1.5 mb-2 border border-input rounded-[var(--radius)] bg-card text-[12px]">
                <option value="user">指定用户</option>
                <option value="role">角色</option>
                <option value="dept_leader">部门主管</option>
              </select>
              <label class="block text-[11px] text-muted-foreground mb-0.5">策略值（用户ID/角色编码）</label>
              <input v-model="editApproverValue" @input="applyToNode"
                class="w-full h-[28px] px-2 mb-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
              <label class="block text-[11px] text-muted-foreground mb-0.5">SLA 小时</label>
              <input v-model="editSlaHours" @input="applyToNode" type="number"
                class="w-full h-[28px] px-2 mb-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
            </template>

            <template v-else-if="selectedNode.type === 'service_task'">
              <label class="block text-[11px] text-muted-foreground mb-0.5">服务类型 (ServiceKind)</label>
              <input v-model="editServiceKind" @input="applyToNode" placeholder="如 create_wms_inbound"
                class="w-full h-[28px] px-2 mb-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
            </template>
          </template>

          <template v-else-if="selectedEdge">
            <div class="text-[12px] font-semibold mb-2">连线条件</div>
            <div class="text-[11px] text-muted-foreground mb-2">{{ selectedEdge.from }} → {{ selectedEdge.to }}</div>
            <ConditionBuilder
              :model-value="selectedEdge.condition ?? ''"
              :fields="parsedFormFields"
              @update:model-value="v => { editCondition = v; applyToEdge() }"
            />
            <p class="text-[10px] text-muted-foreground mt-1.5">排他网关按连线顺序取第一个满足的条件；留空为默认分支。</p>
          </template>

          <div v-else class="text-[11px] text-muted-foreground flex items-center gap-1">
            <MousePointer2 :size="12" />选中节点或连线进行配置
          </div>

          <div class="mt-4 pt-3 border-t border-border">
            <div class="flex items-center justify-between mb-1">
              <span class="text-[11px] text-muted-foreground">表单字段 {{ formFieldCount }} 个</span>
              <button class="text-[11px] text-primary hover:underline border-0 bg-transparent cursor-pointer" @click="formDesignerOpen = true">可视化设计</button>
            </div>
            <details>
              <summary class="text-[10px] text-muted-foreground cursor-pointer select-none">高级 JSON</summary>
              <textarea v-model="formSchema" rows="5"
                class="mt-1 w-full px-2 py-1.5 border border-input rounded-[var(--radius)] bg-card text-[11px] font-mono resize-none focus:outline-none focus:ring-1 focus:ring-ring" />
            </details>
          </div>
        </div>
      </div>
    </div>

    <Sheet v-model:open="formDesignerOpen">
      <SheetContent side="right" class="w-[760px]">
        <SheetHeader><SheetTitle>表单设计</SheetTitle></SheetHeader>
        <div class="p-4 h-[calc(100vh-90px)] overflow-hidden">
          <FormDesigner v-model="formSchema" />
        </div>
      </SheetContent>
    </Sheet>
  </div>
</template>
