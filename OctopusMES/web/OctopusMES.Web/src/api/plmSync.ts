import { createAuthedHttp } from '@/utils/http'
import type { ReconcileSummary, SyncRecordDetail, SyncRecordPage } from '@/views/scheduling/plmSyncTypes'

const http = createAuthedHttp('/api/mes-products')
type Wrapper<T> = { code: number; msg: string; data: T }

export async function getSyncRecords(params: { status?: string; keyword?: string; page: number; pageSize: number }) {
  const response = await http.get<Wrapper<SyncRecordPage>>('/sync-records', { params })
  return response.data
}
export async function getSyncRecordDetail(id: number) {
  const response = await http.get<Wrapper<SyncRecordDetail>>(`/sync-records/${id}`)
  return response.data
}
export async function retrySyncRecord(id: number) {
  const response = await http.post<Wrapper<unknown>>(`/sync-records/${id}/retry`)
  return response.data
}
export async function getReconciliation() {
  const response = await http.get<Wrapper<ReconcileSummary>>('/reconcile')
  return response.data
}
export async function initializeReconciliation() {
  const response = await http.post<Wrapper<ReconcileSummary>>('/initialize')
  return response.data
}
