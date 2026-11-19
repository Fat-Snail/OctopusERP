export interface StatsSummaryResponse {
  supplierCount: number
  purchaseInProgress: number
  workOrderInProgress: number
  workOrderCompleted: number
  totalPurchaseAmount: number
}

// ── Q3-④ 设备 OEE（报工口径） ──

export interface OeeOverall {
  availability: number
  performance: number
  quality: number
  oee: number
}

export interface OeeEquipmentRow {
  equipmentId: number
  equipmentCode: string
  equipmentName: string
  workCenter: string | null
  status: string
  availability: number
  performance: number
  quality: number
  oee: number
  runMinutes: number
  downtimeMinutes: number
  totalQty: number
  qualifiedQty: number
  processCount: number
}

export interface OeeResponse {
  overall: OeeOverall
  equipments: OeeEquipmentRow[]
  note: string
}
