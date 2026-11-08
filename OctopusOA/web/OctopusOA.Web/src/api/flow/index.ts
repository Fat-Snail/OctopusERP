import type { ApiResponse } from '../types'
import type {
  FlowDefinition,
  FlowDefinitionDetail,
  FlowInstance,
  FlowInstanceSummary,
  FlowTask,
  SaveFlowDefinitionRequest,
  SubmitFlowRequest,
} from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/flow')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

// ── 流程定义 ────────────────────────────────────

export async function getFlowDefinitions(): Promise<FlowDefinition[]> {
  return unwrap(await http.get<ApiResponse<FlowDefinition[]>>('/definitions'))
}

export async function getFlowDefinition(id: number): Promise<FlowDefinitionDetail> {
  return unwrap(await http.get<ApiResponse<FlowDefinitionDetail>>(`/definitions/${id}`))
}

export async function saveFlowDefinition(
  data: SaveFlowDefinitionRequest
): Promise<{ id: number; version: number }> {
  return unwrap(await http.post<ApiResponse<{ id: number; version: number }>>('/definitions', data))
}

// ── 发起 / 实例 ──────────────────────────────────

export async function submitFlow(data: SubmitFlowRequest): Promise<{ instanceId: number }> {
  return unwrap(await http.post<ApiResponse<{ instanceId: number }>>('/submit', data))
}

export async function getFlowPending(): Promise<{ rows: FlowTask[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: FlowTask[]; total: number }>>('/pending'))
}

export async function getMyFlows(): Promise<{ rows: FlowInstanceSummary[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: FlowInstanceSummary[]; total: number }>>('/mine'))
}

export async function getFlowInstance(id: number): Promise<FlowInstance> {
  return unwrap(await http.get<ApiResponse<FlowInstance>>(`/${id}`))
}

// ── 任务操作 ────────────────────────────────────

export async function approveFlowTask(taskId: number, comment?: string): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/task/${taskId}/approve`, { comment }))
}

export async function rejectFlowTask(taskId: number, comment?: string): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/task/${taskId}/reject`, { comment }))
}

export async function cancelFlow(instanceId: number): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/${instanceId}/cancel`, {}))
}
