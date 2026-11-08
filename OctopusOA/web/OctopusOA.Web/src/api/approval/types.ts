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
  /// <summary>L3：自定义组件 id（设置后优先用组件渲染，忽略 type）</summary>
  componentId?: string
}

/// <summary>表单扩展视图：L2 远程视图（iframe）/ L3 自定义组件（componentId）</summary>
export interface FormExtension {
  slot?: 'before_form' | 'after_form'
  type: 'iframe' | 'component'
  title?: string
  url?: string
  componentId?: string
  height?: number
}

export interface FormSchema {
  fields: FormField[]
  extensions?: FormExtension[]
}

export type ApprovalStatus = 'draft' | 'pending' | 'approved' | 'rejected' | 'cancelled'
