import type { ApiResponse } from '../types'
import type {
  StatsSummaryResponse,
  PipelineStageItem,
  OverdueItem,
  BiEfficiencyResponse,
  OtdTrendPoint,
  ApprovalBacklogItem,
  ContractTimelineResponse,
  ContractBriefRow,
} from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/stats')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getSummary(): Promise<StatsSummaryResponse> {
  return unwrap(await http.get<ApiResponse<StatsSummaryResponse>>('/summary'))
}

export async function getPipeline(): Promise<PipelineStageItem[]> {
  return unwrap(await http.get<ApiResponse<PipelineStageItem[]>>('/pipeline'))
}

export async function getOverdue(): Promise<OverdueItem[]> {
  return unwrap(await http.get<ApiResponse<OverdueItem[]>>('/overdue'))
}

// BI 看板
export async function getBiEfficiency(): Promise<BiEfficiencyResponse> {
  return unwrap(await http.get<ApiResponse<BiEfficiencyResponse>>('/bi/efficiency'))
}

export async function getOtdTrend(months = 6): Promise<OtdTrendPoint[]> {
  return unwrap(await http.get<ApiResponse<OtdTrendPoint[]>>('/bi/otd-trend', { params: { months } }))
}

export async function getApprovalBacklog(): Promise<ApprovalBacklogItem[]> {
  return unwrap(await http.get<ApiResponse<ApprovalBacklogItem[]>>('/bi/approval-backlog'))
}

export async function getContractTimeline(contractId: number): Promise<ContractTimelineResponse> {
  return unwrap(await http.get<ApiResponse<ContractTimelineResponse>>(`/bi/contract-timeline/${contractId}`))
}

export async function getContractBriefList(): Promise<ContractBriefRow[]> {
  return unwrap(await http.get<ApiResponse<ContractBriefRow[]>>('/bi/contracts'))
}
