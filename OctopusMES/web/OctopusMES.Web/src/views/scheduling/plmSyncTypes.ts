export type SyncStatus = 'pending' | 'syncing' | 'retry_waiting' | 'synced' | 'manual_action' | 'obsolete'

export type SyncRecordRow = {
  syncRecordId: number
  plmProductId: number
  plmManufacturingVersionId?: number
  plmVersion: string
  productCode?: string
  productName?: string
  status: SyncStatus
  payloadHash?: string
  retryCount: number
  nextRetryAt?: string
  syncedAt?: string
  errorMessage?: string
  requiresManualAction: boolean
  manufacturingSnapshotId?: number
}

export type SyncRecordPage = { rows: SyncRecordRow[]; total: number; page: number; pageSize: number; hasMore: boolean }
export type SyncAttempt = { attemptNumber: number; status: string; errorCode?: string; errorMessage?: string; validationIssuesJson?: string; retryable: boolean; startedAt: string; finishedAt: string }
export type SnapshotDetail = { manufacturingSnapshotId: number; productCode: string; productName: string; productVersion: string; bomVersion: string; routingVersion: string; payloadHash: string; status: string; bomItems: Array<{ lineNo: number; materialCode: string; materialName: string; quantity: number; lossRate: number }>; operations: Array<{ sequence: number; operationCode: string; operationName: string; requiredCapability: string; standardCycleSec: number }> }
export type RelatedOrder = { productionOrderId: number; productionOrderCode: string; status: string; plannedQty: number; dueDate?: string }
export type SyncRecordDetail = { record: SyncRecordRow; attempts: SyncAttempt[]; snapshot?: SnapshotDetail; relatedOrders: RelatedOrder[] }
export type ReconcileSummary = { available: boolean; message: string; plmTotal: number; releasedTotal: number; localRecordTotal: number; sameTotal: number; differentTotal: number; syncedTotal: number; unreleasedTotal: number }

export const statusMeta: Record<SyncStatus, { label: string; className: string }> = {
  pending: { label: '待同步', className: 'text-amber-700' },
  syncing: { label: '同步中', className: 'text-blue-700' },
  retry_waiting: { label: '自动重试', className: 'text-orange-700' },
  synced: { label: '同步成功', className: 'text-emerald-700' },
  manual_action: { label: '待人工处理', className: 'text-red-600 font-semibold' },
  obsolete: { label: '已废止', className: 'text-muted-foreground' },
}
