export interface NodeResponse {
  nodeId: number
  nodeName: string
  nodeOrder: number
  approverType: string
  approverValue: string | null
}

export interface TemplateResponse {
  templateId: number
  templateName: string
  templateCode: string
  description: string | null
  icon: string | null
  formSchema: string
  status: number
  createTime: string
  nodes: NodeResponse[]
}

export interface RecordResponse {
  recordId: number
  nodeOrder: number
  nodeName: string
  approverId: number
  approverName: string
  action: string
  comment: string | null
  actionTime: string
}

export interface ApprovalResponse {
  approvalId: number
  templateId: number
  templateName: string
  title: string
  applicantId: number
  applicantName: string
  currentNodeOrder: number
  currentNodeName: string | null
  totalNodes: number
  status: string
  formData: string
  createTime: string
  updateTime: string
  currentNodeDeadline: string | null
  isOverdue: boolean
  records: RecordResponse[]
}

export interface SubmitApprovalRequest {
  templateId: number
  title: string
  formData: string
}
