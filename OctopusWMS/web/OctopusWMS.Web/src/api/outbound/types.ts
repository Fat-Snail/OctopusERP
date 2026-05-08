export type OutboundStatus = 'pending' | 'picking' | 'shipped' | 'cancelled'

export interface OutboundItemRow {
  itemId: number
  plmProductId: number
  productName: string
  sku: string | null
  quantity: number
  unit: string | null
}

export interface OutboundListRow {
  outboundId: number
  outboundCode: string
  warehouseId: number
  warehouseName: string
  status: OutboundStatus
  crmContractId: number | null
  shippedAt: string | null
  remark: string | null
  createdAt: string
}

export interface OutboundDetail extends OutboundListRow {
  items: OutboundItemRow[]
}

export interface CreateOutboundItemRequest {
  plmProductId: number
  productName: string
  sku?: string
  quantity: number
  unit?: string
}

export interface CreateOutboundRequest {
  warehouseId: number
  crmContractId?: number
  remark?: string
  items: CreateOutboundItemRequest[]
}

export interface OutboundQuery {
  warehouseId?: number
  status?: OutboundStatus
}
