export type QuoteStatus = 'draft' | 'submitted' | 'confirmed' | 'rejected' | 'expired'

export interface QuoteListRow {
  quoteId: number
  quoteCode: string
  status: QuoteStatus
  totalAmount: number
  currency: string
  customerId: number
  customerName: string
  inquiryId: number | null
  inquiryCode: string | null
  createdAt: string
}

export interface QuoteDetail extends QuoteListRow {
  remark: string | null
  validUntil: string | null
}

export interface QuoteQuery {
  keyword?: string
  status?: QuoteStatus
  customerId?: number
}
