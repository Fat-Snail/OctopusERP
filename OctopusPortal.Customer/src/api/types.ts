// ── 门户领域类型（Mock 版与未来真实 API 共用同一契约） ──

export type InquiryStatus = 'submitted' | 'quoting' | 'quoted' | 'won'

export interface InquiryTimelineStep {
  label: string
  time: string
  done: boolean
}

export interface Inquiry {
  id: string
  code: string
  productName: string
  sku: string
  quantity: number
  unit: string
  status: InquiryStatus
  quotedAmount?: number
  quotedDeliveryDays?: number
  createdAt: string
  timeline: InquiryTimelineStep[]
}

export type ContractStatus = 'executing' | 'completed'

export interface Contract {
  id: string
  code: string
  productName: string
  amount: number
  status: ContractStatus
  deliveryDate: string
  signedAt: string
}

export type ShipmentStatus = 'preparing' | 'shipping' | 'delivered'

export interface ShipmentTrace {
  time: string
  desc: string
  location?: string
}

export interface Shipment {
  id: string
  trackingNo: string
  carrier: string
  status: ShipmentStatus
  contractCode: string
  productName: string
  traces: ShipmentTrace[]
}

export interface StatementSummary {
  receivable: number
  paid: number
  unpaid: number
}

export type StatementType = 'invoice' | 'payment'
export type StatementStatus = 'pending' | 'settled'

export interface StatementItem {
  id: string
  date: string
  type: StatementType
  code: string
  description: string
  amount: number
  status: StatementStatus
}

export interface ChatMessage {
  id: string
  from: 'me' | 'agent'
  text: string
  time: string
}

export interface DocumentItem {
  id: string
  title: string
  category: string
  size: string
  updatedAt: string
  downloads: number
}

export interface PortalSummary {
  openInquiries: number
  activeContracts: number
  shippingOrders: number
  unpaidAmount: number
}

export interface ApiResponse<T> {
  code: number
  message: string
  data: T
}
