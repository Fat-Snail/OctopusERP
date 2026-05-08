export type PurchaseStatus = 'draft' | 'submitted' | 'approved' | 'rejected' | 'completed'

export interface PurchaseItemRow {
  itemId: number
  plmProductId: number
  productName: string
  sku: string | null
  quantity: number
  unitPrice: number
  totalPrice: number
  unit: string | null
}

export interface PurchaseListRow {
  purchaseOrderId: number
  purchaseCode: string
  supplierId: number
  supplierName: string
  status: PurchaseStatus
  totalAmount: number
  oaApprovalId: number | null
  wmsInboundOrderId: number | null
  expectedDate: string | null
  createdAt: string
}

export interface PurchaseDetail extends PurchaseListRow {
  remark: string | null
  items: PurchaseItemRow[]
}

export interface CreatePurchaseItemRequest {
  plmProductId: number
  productName: string
  sku?: string
  quantity: number
  unitPrice: number
  unit?: string
}

export interface CreatePurchaseRequest {
  supplierId: number
  expectedDate?: string
  remark?: string
  items: CreatePurchaseItemRequest[]
}

export interface PurchaseQuery {
  supplierId?: number
  status?: PurchaseStatus
}
