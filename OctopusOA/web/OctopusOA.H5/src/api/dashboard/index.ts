import { http, unwrap } from '@/utils/http'
import type { PagedResult } from '@/api/types'
import type { DashboardSummary, TodoItem } from './types'

export async function getSummary(): Promise<DashboardSummary> {
  const res = await http.get<{ code: number; msg: string; data: DashboardSummary }>('/dashboard/summary')
  return unwrap(res)
}

export async function getTodos(type?: string): Promise<PagedResult<TodoItem>> {
  const res = await http.get<{ code: number; msg: string; data: PagedResult<TodoItem> }>('/dashboard/todos', { params: type ? { type } : {} })
  return unwrap(res)
}
