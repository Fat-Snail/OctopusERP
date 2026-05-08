export type InquiryStatus = 'open' | 'in_progress' | 'quoted' | 'closed' | 'cancelled'

export interface InquiryListRow {
  inquiryId: number
  inquiryCode: string
  title: string
  status: InquiryStatus
  customerId: number
  customerName: string
  assignedToName: string | null
  createdAt: string
}

export interface CreateInquiryRequest {
  title: string
  customerId: number
  assignedToId?: number
  remark?: string
}

export interface InquiryQuery {
  keyword?: string
  status?: InquiryStatus
  customerId?: number
}
