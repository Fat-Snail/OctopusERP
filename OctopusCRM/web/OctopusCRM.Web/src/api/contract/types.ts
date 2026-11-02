export type ContractStatus = 'draft' | 'submitted' | 'approved' | 'shipped' | 'completed' | 'cancelled'

export interface ContractListRow {
  contractId: number
  contractCode: string
  title: string
  status: ContractStatus
  totalAmount: number
  currency: string
  customerId: number
  customerName: string
  deliveryDate: string | null
  createdAt: string
}

export interface ContractDetail extends ContractListRow {
  remark: string | null
  quoteId: number | null
  quoteCode: string | null
}

export interface ContractQuery {
  keyword?: string
  status?: ContractStatus
  customerId?: number
}
