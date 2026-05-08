export interface InventoryRow {
  inventoryId: number
  warehouseId: number
  warehouseName: string
  plmProductId: number
  productName: string
  sku: string | null
  quantity: number
  reservedQuantity: number
  availableQuantity: number
  unit: string | null
  safetyStock: number
  updatedAt: string
}

export interface InventorySummary {
  totalItems: number
  totalQuantity: number
  lowStockCount: number
}

export interface AdjustInventoryRequest {
  inventoryId: number
  delta: number
  reason: string
}

export interface InventoryQuery {
  warehouseId?: number
  keyword?: string
}
