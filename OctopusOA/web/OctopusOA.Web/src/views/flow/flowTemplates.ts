/**
 * 流程模板库：内置常用审批流程模板。
 * 每个模板提供表单 schema 与图结构（start → 审批节点 → end），选中后作为草稿载入设计器。
 */
import type { FlowGraph } from '@/api/flow/types'
import type { FormSchema } from '@/api/approval/types'

export interface FlowTemplate {
  id: string
  name: string
  code: string
  category: string
  description: string
  icon: string
  formSchema: FormSchema
  graph: FlowGraph
}

function graph(approverName: string, approverValue = 'oa_admin'): FlowGraph {
  return {
    nodes: [
      { id: 'start_1', type: 'start', name: '开始', x: 300, y: 60 },
      { id: 'approval_1', type: 'approval', name: approverName, x: 300, y: 200,
        config: { approverType: 'role', approverValue, slaHours: 24 } },
      { id: 'end_1', type: 'end', name: '结束', x: 300, y: 340 },
    ],
    edges: [
      { from: 'start_1', to: 'approval_1', condition: null },
      { from: 'approval_1', to: 'end_1', condition: null },
    ],
  }
}

function form(fields: FormSchema['fields']): FormSchema {
  return { fields }
}

export const FLOW_TEMPLATES: FlowTemplate[] = [
  {
    id: 'leave', name: '请假申请', code: 'leave', category: '人事', description: '请假天数与事由，主管审批', icon: 'CalendarDays',
    formSchema: form([
      { key: 'leaveType', label: '请假类型', type: 'select', required: true, options: [
        { label: '事假', value: 'personal' }, { label: '病假', value: 'sick' }, { label: '年假', value: 'annual' },
      ] },
      { key: 'days', label: '请假天数', type: 'number', required: true },
      { key: 'reason', label: '请假事由', type: 'textarea', required: true },
    ]),
    graph: graph('直属主管审批'),
  },
  {
    id: 'expense', name: '费用报销', code: 'expense', category: '财务', description: '含明细表与公式合计，财务审核', icon: 'Receipt',
    formSchema: form([
      { key: 'title', label: '报销标题', type: 'text', required: true },
      { key: 'items', label: '费用明细', type: 'subform', required: true, subFields: [
        { key: 'name', label: '项目', type: 'text' },
        { key: 'amount', label: '金额', type: 'number' },
      ] },
      { key: 'total', label: '合计金额', type: 'number', formula: '0' },
      { key: 'remark', label: '备注', type: 'textarea' },
    ]),
    graph: graph('财务审核'),
  },
  {
    id: 'purchase', name: '采购申请', code: 'purchase', category: '采购', description: '金额超 10 万走总经理审批（条件分支示例）', icon: 'ShoppingCart',
    formSchema: form([
      { key: 'product', label: '采购物品', type: 'text', required: true },
      { key: 'quantity', label: '数量', type: 'number', required: true },
      { key: 'unitPrice', label: '单价', type: 'number', required: true },
      { key: 'amount', label: '总金额', type: 'number', formula: 'quantity * unitPrice' },
    ]),
    graph: {
      nodes: [
        { id: 'start_1', type: 'start', name: '开始', x: 300, y: 40 },
        { id: 'gateway_1', type: 'exclusive_gateway', name: '金额判断', x: 300, y: 140 },
        { id: 'approval_mgr', type: 'approval', name: '部门经理审批', x: 140, y: 260, config: { approverType: 'role', approverValue: 'oa_dept_manager', slaHours: 24 } },
        { id: 'approval_boss', type: 'approval', name: '总经理审批', x: 460, y: 260, config: { approverType: 'role', approverValue: 'oa_admin', slaHours: 48 } },
        { id: 'end_1', type: 'end', name: '结束', x: 300, y: 400 },
      ],
      edges: [
        { from: 'start_1', to: 'gateway_1', condition: null },
        { from: 'gateway_1', to: 'approval_boss', condition: 'amount > 100000' },
        { from: 'gateway_1', to: 'approval_mgr', condition: null },
        { from: 'approval_mgr', to: 'end_1', condition: null },
        { from: 'approval_boss', to: 'end_1', condition: null },
      ],
    },
  },
  {
    id: 'contract', name: '合同审批', code: 'contract', category: '业务', description: '合同金额与对方单位，法务+财务会签', icon: 'FileText',
    formSchema: form([
      { key: 'contractName', label: '合同名称', type: 'text', required: true },
      { key: 'party', label: '对方单位', type: 'text', required: true },
      { key: 'amount', label: '合同金额', type: 'number', required: true },
      { key: 'file', label: '合同附件', type: 'file' },
    ]),
    graph: graph('法务审核'),
  },
  {
    id: 'resignation', name: '离职申请', code: 'resignation', category: '人事', description: '离职原因与交接，主管+HR 审批', icon: 'LogOut',
    formSchema: form([
      { key: 'lastDay', label: '预计离职日期', type: 'date', required: true },
      { key: 'reason', label: '离职原因', type: 'textarea', required: true },
      { key: 'handover', label: '交接说明', type: 'textarea' },
    ]),
    graph: graph('主管审批'),
  },
]

/** 草稿在 sessionStorage 中的键；设计器 new 路由读取后消费 */
export const FLOW_TEMPLATE_DRAFT_KEY = 'octopus-flow-template-draft'

export interface FlowTemplateDraft {
  code: string
  name: string
  category: string
  formSchema: string
  graphJson: string
}
