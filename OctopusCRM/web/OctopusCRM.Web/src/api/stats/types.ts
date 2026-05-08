export interface StatsSummaryResponse {
  totalCustomers: number
  openInquiries: number
  activeContracts: number
  totalContractAmount: number
  currency: string
}

export interface PipelineStageItem {
  stage: string
  count: number
  amount: number
}

export interface OverdueItem {
  contractId: number
  contractCode: string
  customerName: string
  deliveryDate: string
  totalAmount: number
  currency: string
}

// BI 看板专用类型
export interface BiEfficiencyResponse {
  inquiryToQuoteHours: number
  quoteApprovalDays: number
  contractCycleDays: number
  otdRate: number
  supplierDeliveryRate: number
  approvalOverdueRate: number
  dso: number
}

export interface OtdTrendPoint {
  month: string
  otdRate: number
  total: number
  onTime: number
}

export interface ApprovalBacklogItem {
  stage: string
  label: string
  pendingCount: number
  overdueCount: number
}

export interface ContractTimelineEvent {
  stage: string
  label: string
  eventTime: string | null
  isCompleted: boolean
  remark: string | null
}

export interface ContractTimelineResponse {
  contractId: number
  contractCode: string
  customerName: string
  events: ContractTimelineEvent[]
}

export interface ContractBriefRow {
  contractId: number
  contractCode: string
  customerName: string
  status: string
}
