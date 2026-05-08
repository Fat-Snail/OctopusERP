export type WorkOrderStatus = 'draft' | 'in_progress' | 'completed' | 'cancelled'

export interface WorkOrderProcessRow {
  processId: number
  processName: string
  processOrder: number
  status: string
  startedAt: string | null
  completedAt: string | null
  remark: string | null
}

export interface WorkOrderListRow {
  workOrderId: number
  workOrderCode: string
  plmProductId: number
  productName: string
  plannedQuantity: number
  completedQuantity: number
  status: WorkOrderStatus
  plannedStartDate: string | null
  plannedEndDate: string | null
  createdAt: string
}

export interface WorkOrderDetail extends WorkOrderListRow {
  remark: string | null
  processes: WorkOrderProcessRow[]
}

export interface CreateWorkOrderRequest {
  plmProductId: number
  productName: string
  plannedQuantity: number
  plannedStartDate?: string
  plannedEndDate?: string
  remark?: string
}

export interface WorkOrderQuery {
  status?: WorkOrderStatus
  plmProductId?: number
}
