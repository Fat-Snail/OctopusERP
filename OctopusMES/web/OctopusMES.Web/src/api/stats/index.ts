import type { ApiResponse } from '../types'
import type { StatsSummaryResponse, OeeResponse } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/stats')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getSummary(): Promise<StatsSummaryResponse> {
  return unwrap(await http.get<ApiResponse<StatsSummaryResponse>>('/summary'))
}

export async function getOee(params?: { workCenter?: string; from?: string; to?: string }): Promise<OeeResponse> {
  return unwrap(await http.get<ApiResponse<OeeResponse>>('/oee', { params }))
}
