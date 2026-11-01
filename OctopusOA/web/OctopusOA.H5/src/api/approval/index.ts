import { http, unwrap } from '@/utils/http'
import type { PagedResult } from '@/api/types'
import type { ApprovalResponse, TemplateResponse, SubmitApprovalRequest } from './types'

export async function getMyApprovals(): Promise<PagedResult<ApprovalResponse>> {
  const res = await http.get<{ code: number; msg: string; data: PagedResult<ApprovalResponse> }>('/approval/mine')
  return unwrap(res)
}

export async function getPendingApprovals(): Promise<PagedResult<ApprovalResponse>> {
  const res = await http.get<{ code: number; msg: string; data: PagedResult<ApprovalResponse> }>('/approval/pending')
  return unwrap(res)
}

export async function getAvailableTemplates(): Promise<TemplateResponse[]> {
  const res = await http.get<{ code: number; msg: string; data: TemplateResponse[] }>('/approval/templates')
  return unwrap(res)
}

export async function getTemplateById(id: number): Promise<TemplateResponse> {
  const res = await http.get<{ code: number; msg: string; data: TemplateResponse }>(`/approval/template/${id}`)
  return unwrap(res)
}

export async function getApprovalById(id: number): Promise<ApprovalResponse> {
  const res = await http.get<{ code: number; msg: string; data: ApprovalResponse }>(`/approval/${id}`)
  return unwrap(res)
}

export async function submitApproval(req: SubmitApprovalRequest): Promise<void> {
  const res = await http.post<{ code: number; msg: string; data: null }>('/approval/submit', req)
  unwrap(res)
}

export async function approveApproval(id: number, comment?: string): Promise<void> {
  const res = await http.put<{ code: number; msg: string; data: null }>(`/approval/${id}/approve`, { comment })
  unwrap(res)
}

export async function rejectApproval(id: number, comment?: string): Promise<void> {
  const res = await http.put<{ code: number; msg: string; data: null }>(`/approval/${id}/reject`, { comment })
  unwrap(res)
}

export async function cancelApproval(id: number): Promise<void> {
  const res = await http.put<{ code: number; msg: string; data: null }>(`/approval/${id}/cancel`, {})
  unwrap(res)
}
