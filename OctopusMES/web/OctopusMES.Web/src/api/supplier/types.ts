export type SupplierLevel = 'A' | 'B' | 'C'
export type SupplierStatus = 'active' | 'inactive'

export interface SupplierListRow {
  supplierId: number
  supplierCode: string
  supplierName: string
  contactPerson: string | null
  contactPhone: string | null
  level: SupplierLevel
  status: SupplierStatus
  createdAt: string
}

export interface SupplierDetail extends SupplierListRow {
  address: string | null
  remark: string | null
}

export interface CreateSupplierRequest {
  supplierName: string
  contactPerson?: string
  contactPhone?: string
  level: SupplierLevel
  status: SupplierStatus
  address?: string
  remark?: string
}

export interface UpdateSupplierRequest extends CreateSupplierRequest {
  supplierId: number
}

export interface SupplierQuery {
  keyword?: string
  level?: SupplierLevel
  status?: SupplierStatus
}
