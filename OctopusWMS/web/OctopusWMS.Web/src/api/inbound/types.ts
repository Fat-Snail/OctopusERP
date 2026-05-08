export type InboundStatus = 'pending' | 'partial' | 'completed' | 'cancelled'

export interface InboundItemRow {
  itemId: number
  plmProductId: number
  productName: string
  sku: string | null
  expectedQuantity: number
  receivedQuantity: number
  unit: string | null
}

export interface InboundListRow {
  inboundId: number
  inboundCode: string
  warehouseId: number
  warehouseName: string
  status: InboundStatus
  expectedDate: string | null
  completedAt: string | null
  remark: string | null
  createdAt: string
}

export interface InboundDetail extends InboundListRow {
  items: InboundItemRow[]
}

export interface CreateInboundItemRequest {
  plmProductId: number
  productName: string
  sku?: string
  expectedQuantity: number
  unit?: string
}

export interface CreateInboundRequest {
  warehouseId: number
  expectedDate?: string
  remark?: string
  items: CreateInboundItemRequest[]
}

export interface ReceiveInboundRequest {
  inboundId: number
  items: { itemId: number; receivedQuantity: number }[]
}

export interface InboundQuery {
  warehouseId?: number
  status?: InboundStatus
}
