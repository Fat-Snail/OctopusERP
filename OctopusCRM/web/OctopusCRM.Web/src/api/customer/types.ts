export type CustomerLevel = 'A' | 'B' | 'C'
export type CustomerStatus = 'active' | 'prospect' | 'inactive'

export interface ContactRow {
  contactId: number
  name: string
  title: string | null
  phone: string | null
  email: string | null
  isPrimary: boolean
}

export interface CustomerListRow {
  customerId: number
  customerCode: string
  customerName: string
  industryType: string | null
  level: CustomerLevel
  status: CustomerStatus
  createdAt: string
}

export interface CustomerDetail extends CustomerListRow {
  address: string | null
  website: string | null
  remark: string | null
  contacts: ContactRow[]
}

export interface CreateCustomerRequest {
  customerName: string
  industryType?: string
  level: CustomerLevel
  status: CustomerStatus
  address?: string
  website?: string
  remark?: string
}

export interface UpdateCustomerRequest extends CreateCustomerRequest {
  customerId: number
}

export interface CreateContactRequest {
  customerId: number
  name: string
  title?: string
  phone?: string
  email?: string
  isPrimary: boolean
}

export interface CustomerQuery {
  keyword?: string
  level?: CustomerLevel
  status?: CustomerStatus
}
