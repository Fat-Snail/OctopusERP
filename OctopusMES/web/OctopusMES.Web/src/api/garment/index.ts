import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type {
  GarmentTeam,
  GarmentCalendar,
  GarmentScheduleListRow,
  GarmentMonthlySummaryRow,
  GarmentScheduleQuery,
  GarmentArchiveSummary,
  CandidateLeader,
  UpsertGarmentTeamRequest,
} from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/garment-schedule')
const teamHttp = createAuthedHttp('/api/garment-team')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getGarmentTeams(): Promise<{ rows: GarmentTeam[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: GarmentTeam[]; total: number }>>('/teams'))
}

export async function getGarmentCalendar(params: {
  startDate: string
  endDate: string
  season?: string
}): Promise<GarmentCalendar> {
  return unwrap(await http.get<ApiResponse<GarmentCalendar>>('/calendar', { params }))
}

export async function getGarmentScheduleList(
  params: GarmentScheduleQuery & PageQuery,
): Promise<PagedResult<GarmentScheduleListRow>> {
  return unwrap(
    await http.get<ApiResponse<PagedResult<GarmentScheduleListRow>>>('', { params }),
  )
}

export async function saveGarmentSchedule(data: Record<string, unknown>): Promise<GarmentScheduleListRow> {
  return unwrap(await http.post<ApiResponse<GarmentScheduleListRow>>('', data))
}

export interface BatchCreateScheduleRequest {
  teamId: number
  dates: string[] // ISO date strings yyyy-MM-dd
  season?: string
  customer?: string | null
  customerId?: number | null
  orderNo?: string | null
  contractId?: number | null
  styleCode?: string | null
  plmProductId?: number | null
  dueDate?: string | null
  plannedQty?: number | null
  fabricStatus?: string | null
  remark?: string | null
}

export interface BatchCreateScheduleResult {
  created: number
  updated: number
  total: number
  scheduleIds: number[]
}

export async function batchCreateGarmentSchedule(
  data: BatchCreateScheduleRequest,
): Promise<BatchCreateScheduleResult> {
  return unwrap(await http.post<ApiResponse<BatchCreateScheduleResult>>('/batch', data))
}

/** 归档概览：按年→月聚合早于 archiveCutoff 的排程条数 */
export async function getGarmentArchiveSummary(): Promise<GarmentArchiveSummary> {
  return unwrap(await http.get<ApiResponse<GarmentArchiveSummary>>('/archive/summary'))
}

export async function deleteGarmentSchedule(id: number): Promise<null> {
  return unwrap(await http.delete<ApiResponse<null>>(`/${id}`))
}

export async function getGarmentMonthlySummary(
  season?: string,
): Promise<{ rows: GarmentMonthlySummaryRow[]; total: number }> {
  return unwrap(
    await http.get<ApiResponse<{ rows: GarmentMonthlySummaryRow[]; total: number }>>(
      '/monthly-summary',
      { params: { season } },
    ),
  )
}

// ── 车间组管理 ────────────────────────────────────────────

export async function listGarmentTeams(params: {
  keyword?: string
  status?: string
  includeInactive?: boolean
} = {}): Promise<{ rows: GarmentTeam[]; total: number }> {
  return unwrap(await teamHttp.get<ApiResponse<{ rows: GarmentTeam[]; total: number }>>('', { params }))
}

export async function createGarmentTeam(data: UpsertGarmentTeamRequest): Promise<GarmentTeam> {
  return unwrap(await teamHttp.post<ApiResponse<GarmentTeam>>('', data))
}

export async function updateGarmentTeam(data: UpsertGarmentTeamRequest): Promise<GarmentTeam> {
  return unwrap(await teamHttp.put<ApiResponse<GarmentTeam>>('', data))
}

export async function setGarmentTeamStatus(id: number, status: 'active' | 'inactive'): Promise<GarmentTeam> {
  return unwrap(await teamHttp.put<ApiResponse<GarmentTeam>>(`/${id}/status`, { status }))
}

export async function getCandidateLeaders(): Promise<{ rows: CandidateLeader[]; total: number }> {
  return unwrap(await teamHttp.get<ApiResponse<{ rows: CandidateLeader[]; total: number }>>('/candidate-leaders'))
}
