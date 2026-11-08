// ── 审批中心（图流程）类型 ──────────────────────────

export interface FlowNodeConfig {
  approverType?: string
  approverValue?: string | null
  slaHours?: number | null
  serviceKind?: string | null
  passRule?: string | null
}

export interface FlowGraphNode {
  id: string
  type: string
  name: string
  x?: number | null
  y?: number | null
  config?: FlowNodeConfig | null
}

export interface FlowGraphEdge {
  from: string
  to: string
  condition?: string | null
}

export interface FlowGraph {
  nodes: FlowGraphNode[]
  edges: FlowGraphEdge[]
}

export interface FlowDefinition {
  id: number
  code: string
  name: string
  version: number
  category: string | null
  businessKey: string | null
  formSchema: string
}

export interface FlowDefinitionDetail extends FlowDefinition {
  graphJson: string
  status: number
}

export interface FlowTask {
  taskId: number
  instanceId: number
  nodeId: string
  nodeName: string
  title: string
  applicantId: number
  applicantName: string
  businessKey: string | null
  instanceStatus: string
  deadline: string | null
  createTime: string
}

export interface FlowTaskDetail {
  taskId: number
  nodeId: string
  nodeName: string
  assigneeId: number
  assigneeName: string
  status: string
  action: string | null
  comment: string | null
  actionTime: string | null
}

export interface FlowHistory {
  nodeName: string
  action: string
  actorId: number
  actorName: string
  comment: string | null
  createTime: string
}

export interface FlowInstance {
  instanceId: number
  definitionId: number
  title: string
  applicantId: number
  applicantName: string
  status: string
  businessKey: string | null
  formData: string
  createTime: string
  updateTime: string
  deadline: string | null
  isOverdue: boolean
  tasks: FlowTaskDetail[]
  history: FlowHistory[]
}

export interface FlowInstanceSummary {
  id: number
  definitionId: number
  title: string
  applicantId: number
  applicantName: string
  status: string
  businessKey: string | null
  createTime: string
  updateTime: string
  deadline: string | null
  isOverdue: boolean
}

export interface SubmitFlowRequest {
  definitionId?: number
  definitionCode?: string
  title: string
  formData: string
  businessKey?: string
}

export interface SaveFlowDefinitionRequest {
  id?: number
  code: string
  name: string
  category?: string
  businessKey?: string
  formSchema: string
  graphJson: string
  status?: number
}
