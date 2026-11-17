// ── 门户领域类型（Mock 版与未来真实 API 共用同一契约） ──

export type QuoteStatus = 'pending' | 'responded' | 'closed'

export interface QuoteRequest {
  id: string
  code: string
  productName: string
  sku: string
  quantity: number
  unit: string
  deadline: string
  status: QuoteStatus
  myQuote?: {
    unitPrice: number
    deliveryDays: number
    remark?: string
    quotedAt: string
    status: 'under_review' | 'accepted' | 'rejected'
  }
}

export type OrderStatus = 'preparing' | 'producing' | 'ready' | 'shipped' | 'completed'

export interface PurchaseOrder {
  id: string
  code: string
  productName: string
  quantity: number
  unit: string
  amount: number
  status: OrderStatus
  planDeliveryDate: string
  createdAt: string
}

export interface Qualification {
  id: string
  title: string
  category: string
  fileName: string
  fileSize: string
  status: 'approved' | 'pending' | 'expired'
  uploadedAt: string
  validUntil?: string
}

export interface StatementSummary {
  payable: number
  settled: number
  outstanding: number
}

export type StatementStatus = 'pending' | 'settled'

export interface StatementItem {
  id: string
  date: string
  code: string
  description: string
  amount: number
  status: StatementStatus
}

export interface ShipmentRecord {
  id: string
  orderCode: string
  trackingNo: string
  carrier: string
  shippedAt: string
}

export interface PortalSummary {
  pendingQuotes: number
  activeOrders: number
  readyToShip: number
  outstandingAmount: number
}

export interface ApiResponse<T> {
  code: number
  message: string
  data: T
}
