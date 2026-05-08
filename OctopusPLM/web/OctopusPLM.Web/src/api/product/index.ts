import type { ApiResponse } from '../types'
import type {
  ProductListResult,
  ProductResponse,
  CreateProductRequest,
  UpdateProductRequest,
  ReviewActionRequest,
  ReviewHistoryItem,
  ProductStatsResponse,
  ImageSearchResult,
  Import1688Result,
} from './types'
import { createAuthedHttp } from '@/utils/plmHttp'

const http = createAuthedHttp('/api')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

/** 商品列表（分页 + 筛选） */
export async function getProductList(params: {
  page?: number
  size?: number
  categoryId?: number
  keyword?: string
  status?: string
}): Promise<ProductListResult> {
  return unwrap(await http.get<ApiResponse<ProductListResult>>('/product/list', { params }))
}

/** 商品详情 */
export async function getProductById(id: number): Promise<ProductResponse> {
  return unwrap(await http.get<ApiResponse<ProductResponse>>(`/product/${id}`))
}

/** 创建商品 */
export async function createProduct(data: CreateProductRequest): Promise<ProductResponse> {
  return unwrap(await http.post<ApiResponse<ProductResponse>>('/product', data))
}

/** 修改商品 */
export async function updateProduct(id: number, data: UpdateProductRequest): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/product/${id}`, data))
}

/** 提交审核 */
export async function submitProduct(id: number): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/product/${id}/submit`, {}))
}

/** 审核通过 */
export async function approveProduct(id: number, data: ReviewActionRequest = {}): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/product/${id}/approve`, data))
}

/** 审核驳回 */
export async function rejectProduct(id: number, data: ReviewActionRequest): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/product/${id}/reject`, data))
}

/** 上架 */
export async function publishProduct(id: number): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/product/${id}/publish`, {}))
}

/** 下架 */
export async function discontinueProduct(id: number, data: ReviewActionRequest = {}): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/product/${id}/discontinue`, data))
}

/** 删除商品 */
export async function deleteProduct(id: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/product/${id}`))
}

/** 审核历史 */
export async function getProductReviews(id: number): Promise<ReviewHistoryItem[]> {
  return unwrap(await http.get<ApiResponse<ReviewHistoryItem[]>>(`/product/${id}/reviews`))
}

/** 商品各状态统计 */
export async function getProductStats(): Promise<ProductStatsResponse> {
  return unwrap(await http.get<ApiResponse<ProductStatsResponse>>('/product/stats'))
}

/** 以图搜商品 */
export async function searchByImage(file: File, limit = 10): Promise<ImageSearchResult> {
  const form = new FormData()
  form.append('image', file)
  return unwrap(await http.post<ApiResponse<ImageSearchResult>>(`/product/search/image?limit=${limit}`, form))
}

/** 从1688批量导入商品 */
export async function importFrom1688(productJsonList: string[]): Promise<Import1688Result> {
  return unwrap(await http.post<ApiResponse<Import1688Result>>('/product/import/1688', { productJsonList }))
}
