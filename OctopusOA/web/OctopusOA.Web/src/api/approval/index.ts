import type { ApiResponse } from '../types'
import type {
  TemplateResponse,
  ApprovalResponse,
  SubmitApprovalRequest,
  CreateTemplateRequest,
  UpdateTemplateRequest,
  NodeRequest,
} from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/approval')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

// ── 模板 ────────────────────────────────────────

export async function getTemplateList(): Promise<TemplateResponse[]> {
  return unwrap(await http.get<ApiResponse<TemplateResponse[]>>('/template/list'))
}

export async function getTemplateById(id: number): Promise<TemplateResponse> {
  return unwrap(await http.get<ApiResponse<TemplateResponse>>(`/template/${id}`))
}

export async function createTemplate(data: CreateTemplateRequest): Promise<TemplateResponse> {
  return unwrap(await http.post<ApiResponse<TemplateResponse>>('/template', data))
}

export async function updateTemplate(data: UpdateTemplateRequest): Promise<TemplateResponse> {
  return unwrap(await http.put<ApiResponse<TemplateResponse>>('/template', data))
}

export async function deleteTemplate(id: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/template/${id}`))
}

export async function setTemplateNodes(templateId: number, nodes: NodeRequest[]): Promise<TemplateResponse> {
  return unwrap(await http.post<ApiResponse<TemplateResponse>>(`/template/${templateId}/nodes`, { nodes }))
}

// ── 审批操作 ────────────────────────────────────

export interface AvailableTemplate {
  templateId: number
  templateName: string
  templateCode: string
  description: string | null
  icon: string | null
  formSchema: string
}

export async function getAvailableTemplates(): Promise<AvailableTemplate[]> {
  return unwrap(await http.get<ApiResponse<AvailableTemplate[]>>('/templates'))
}

export async function submitApproval(data: SubmitApprovalRequest): Promise<ApprovalResponse> {
  return unwrap(await http.post<ApiResponse<ApprovalResponse>>('/submit', data))
}

export async function getMyApprovals(): Promise<{ rows: ApprovalResponse[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: ApprovalResponse[]; total: number }>>('/mine'))
}

export async function getPendingApprovals(): Promise<{ rows: ApprovalResponse[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: ApprovalResponse[]; total: number }>>('/pending'))
}

export async function getAllApprovals(): Promise<{ rows: ApprovalResponse[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: ApprovalResponse[]; total: number }>>('/all'))
}

export async function getApprovalById(id: number): Promise<ApprovalResponse> {
  return unwrap(await http.get<ApiResponse<ApprovalResponse>>(`/${id}`))
}

export async function approveApproval(id: number, comment?: string): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/${id}/approve`, { comment }))
}

export async function rejectApproval(id: number, comment?: string): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/${id}/reject`, { comment }))
}

export async function cancelApproval(id: number): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/${id}/cancel`, {}))
}
