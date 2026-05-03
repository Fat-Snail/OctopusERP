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
  records: RecordResponse[]
}

export interface SubmitApprovalRequest {
  templateId: number
  title: string
  formData: string
}

export interface CreateTemplateRequest {
  templateName: string
  templateCode: string
  description?: string
  icon?: string
  formSchema: string
  status: number
}

export interface UpdateTemplateRequest extends CreateTemplateRequest {
  templateId: number
}

export interface NodeRequest {
  nodeName: string
  nodeOrder: number
  approverType: string
  approverValue?: string
}

export interface FormField {
  key: string
  label: string
  type: 'select' | 'date' | 'number' | 'textarea' | 'text' | 'file'
  options?: { label: string; value: string }[]
  required?: boolean
}

export interface FormSchema {
  fields: FormField[]
}

export type ApprovalStatus = 'draft' | 'pending' | 'approved' | 'rejected' | 'cancelled'
