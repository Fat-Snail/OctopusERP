import type { ApiResponse } from '../types'
import type {
  CategoryTreeNode,
  CategoryAttributeGrouped,
  CategoryModelDetailResponse,
  CategoryModelVersionSummary,
  CreateCategoryRequest,
  UpdateCategoryRequest,
  PlmAttribute,
  CreateAttributeRequest,
  UpdateAttributeRequest,
  AddAttributeValueRequest,
  BindAttributeRequest,
  CreateModelVersionRequest,
} from './types'
import { createAuthedHttp } from '@/utils/plmHttp'

const http = createAuthedHttp('/api')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

// ── Category ──────────────────────────────────────────────────────────────────

/** 获取类目树 */
export async function getCategoryTree(): Promise<CategoryTreeNode[]> {
  return unwrap(await http.get<ApiResponse<CategoryTreeNode[]>>('/category/tree'))
}

/** 创建类目 */
export async function createCategory(data: CreateCategoryRequest): Promise<{ categoryId: number }> {
  return unwrap(await http.post<ApiResponse<{ categoryId: number }>>('/category', data))
}

/** 更新类目 */
export async function updateCategory(id: number, data: UpdateCategoryRequest): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/category/${id}`, data))
}

/** 删除类目 */
export async function deleteCategory(id: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/category/${id}`))
}

/** 获取类目下绑定的属性（含枚举值，按 GroupName 分组） */
export async function getCategoryAttributes(id: number): Promise<CategoryAttributeGrouped[]> {
  return unwrap(await http.get<ApiResponse<CategoryAttributeGrouped[]>>(`/category/${id}/attributes`))
}

/** 获取类目当前生效模型详情 */
export async function getCategoryModel(id: number): Promise<CategoryModelDetailResponse> {
  return unwrap(await http.get<ApiResponse<CategoryModelDetailResponse>>(`/category/${id}/model`))
}

/** 获取类目模型版本列表 */
export async function getCategoryModelVersions(id: number): Promise<CategoryModelVersionSummary[]> {
  return unwrap(await http.get<ApiResponse<CategoryModelVersionSummary[]>>(`/category/${id}/model/versions`))
}

// ── Attribute ─────────────────────────────────────────────────────────────────

/** 所有属性（含枚举值） */
export async function getAttributeList(): Promise<PlmAttribute[]> {
  return unwrap(await http.get<ApiResponse<PlmAttribute[]>>('/attribute/list'))
}

/** 创建属性 */
export async function createAttribute(data: CreateAttributeRequest): Promise<{ attributeId: number }> {
  return unwrap(await http.post<ApiResponse<{ attributeId: number }>>('/attribute', data))
}

/** 更新属性 */
export async function updateAttribute(id: number, data: UpdateAttributeRequest): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/attribute/${id}`, data))
}

/** 删除属性 */
export async function deleteAttribute(id: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/attribute/${id}`))
}

/** 添加枚举值 */
export async function addAttributeValue(attributeId: number, data: AddAttributeValueRequest): Promise<{ valueId: number }> {
  return unwrap(await http.post<ApiResponse<{ valueId: number }>>(`/attribute/${attributeId}/values`, data))
}

/** 删除枚举值 */
export async function deleteAttributeValue(valueId: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/attribute/values/${valueId}`))
}

// ── Category attribute bindings (active version) ─────────────────────────────

/** 绑定属性到类目（active 版本） */
export async function bindCategoryAttribute(categoryId: number, data: BindAttributeRequest): Promise<{ id: number }> {
  return unwrap(await http.post<ApiResponse<{ id: number }>>(`/category/${categoryId}/attributes`, data))
}

/** 解绑类目属性（active 版本） */
export async function unbindCategoryAttribute(categoryId: number, bindId: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/category/${categoryId}/attributes/${bindId}`))
}

// ── Model version management ──────────────────────────────────────────────────

/** 获取指定版本的属性 */
export async function getVersionAttributes(categoryId: number, versionId: number): Promise<CategoryAttributeGrouped[]> {
  return unwrap(await http.get<ApiResponse<CategoryAttributeGrouped[]>>(`/category/${categoryId}/model/versions/${versionId}/attributes`))
}

/** 新建草稿版本（克隆 active 属性） */
export async function createModelVersion(categoryId: number, data: CreateModelVersionRequest): Promise<{ modelVersionId: number; versionNo: string }> {
  return unwrap(await http.post<ApiResponse<{ modelVersionId: number; versionNo: string }>>(`/category/${categoryId}/model/versions`, data))
}

/** 发布草稿版本 */
export async function publishModelVersion(categoryId: number, versionId: number): Promise<void> {
  unwrap(await http.post<ApiResponse<null>>(`/category/${categoryId}/model/versions/${versionId}/publish`, {}))
}

/** 删除草稿版本 */
export async function deleteModelVersion(categoryId: number, versionId: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/category/${categoryId}/model/versions/${versionId}`))
}

/** 向草稿版本绑定属性 */
export async function bindAttributeToVersion(categoryId: number, versionId: number, data: BindAttributeRequest): Promise<{ id: number }> {
  return unwrap(await http.post<ApiResponse<{ id: number }>>(`/category/${categoryId}/model/versions/${versionId}/attributes`, data))
}

/** 从草稿版本解绑属性 */
export async function unbindAttributeFromVersion(categoryId: number, versionId: number, bindId: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/category/${categoryId}/model/versions/${versionId}/attributes/${bindId}`))
}
