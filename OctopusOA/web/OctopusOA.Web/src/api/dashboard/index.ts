import type { ApiResponse } from '../types'
import type { DashboardSummary, TodoItem } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/dashboard')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getSummary(): Promise<DashboardSummary> {
  return unwrap(await http.get<ApiResponse<DashboardSummary>>('/summary'))
}

export async function getTodos(type?: string): Promise<{ rows: TodoItem[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: TodoItem[]; total: number }>>('/todos', { params: type ? { type } : {} }))
}
