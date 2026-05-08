export type WarehouseStatus = 'active' | 'inactive'

export interface WarehouseListRow {
  warehouseId: number
  warehouseCode: string
  warehouseName: string
  location: string | null
  contactPerson: string | null
  contactPhone: string | null
  status: WarehouseStatus
  createdAt: string
}

export interface WarehouseDetail extends WarehouseListRow {
  remark: string | null
}

export interface CreateWarehouseRequest {
  warehouseName: string
  location?: string
  contactPerson?: string
  contactPhone?: string
  status: WarehouseStatus
  remark?: string
}

export interface UpdateWarehouseRequest extends CreateWarehouseRequest {
  warehouseId: number
}

export interface WarehouseQuery {
  keyword?: string
  status?: WarehouseStatus
}
