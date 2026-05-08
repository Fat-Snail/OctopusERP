export interface ApiResponse<T = unknown> {
  code: number
  msg: string
  data: T
}

export interface PagedResult<T> {
  rows: T[]
  total: number
}

export interface PageQuery {
  pageNum: number
  pageSize: number
}

export type Status = 0 | 1
