export type StocktakeStatus = 'pending' | 'in_progress' | 'completed' | 'cancelled'

export interface StocktakeItemRow {
  itemId: number
  inventoryId: number
  plmProductId: number
  productName: string
  systemQuantity: number
  actualQuantity: number | null
  difference: number | null
}

export interface StocktakeListRow {
  stocktakeId: number
  stocktakeCode: string
  warehouseId: number
  warehouseName: string
  status: StocktakeStatus
  completedAt: string | null
  remark: string | null
  createdAt: string
}

export interface StocktakeDetail extends StocktakeListRow {
  items: StocktakeItemRow[]
}

export interface SubmitStocktakeRequest {
  stocktakeId: number
  items: { itemId: number; actualQuantity: number }[]
}

export interface StocktakeQuery {
  warehouseId?: number
  status?: StocktakeStatus
}
