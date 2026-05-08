/** 对应后端统一响应包装 */
export interface ApiResponse<T = unknown> {
  code: number
  msg: string
  data: T
}

/** 对应后端分页响应 */
export interface PagedResult<T> {
  rows: T[]
  total: number
}
