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

/** 明细表列定义 */
export interface SubField {
  key: string
  label: string
  type: 'text' | 'number' | 'date' | 'select'
  options?: { label: string; value: string }[]
}

/** 关联数据下拉：从后端字典/接口异步加载选项 */
export interface OptionsFrom {
  /** 数据源：dict:<字典码>（走 /api/system/dict/data）或 http 路径（走 /api 前缀的 authed http） */
  source: string
  labelField?: string
  valueField?: string
}

export interface FormField {
  key: string
  label: string
  type: 'select' | 'date' | 'number' | 'textarea' | 'text' | 'file' | 'subform'
  options?: { label: string; value: string }[]
  required?: boolean
  /// <summary>L3：自定义组件 id（设置后优先用组件渲染，忽略 type）</summary>
  componentId?: string
  /// <summary>字段联动：表达式为真时才显示该字段（如 amount > 1000）</summary>
  visibleWhen?: string
  /// <summary>公式字段：表达式结果自动写入该字段（如 quantity * price），只读展示</summary>
  formula?: string
  /// <summary>明细表：type=subform 时的列定义</summary>
  subFields?: SubField[]
  /// <summary>关联数据下拉：type=select 时可从数据源异步加载选项</summary>
  optionsFrom?: OptionsFrom
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
