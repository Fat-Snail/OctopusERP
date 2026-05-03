import { get, post, put, del } from '@/utils/http'
import type { PagedResult, PageQuery } from '../types'
import type {
  DictTypeResponse,
  DictDataResponse,
  CreateDictTypeRequest,
  UpdateDictTypeRequest,
  CreateDictDataRequest,
  UpdateDictDataRequest
} from './types'

interface DictTypeQuery extends PageQuery {
  dictName?: string
  dictType?: string
  status?: 0 | 1
}

interface DictDataQuery extends PageQuery {
  dictType?: string
  dictLabel?: string
  status?: 0 | 1
}

// ─── 字典类型 ────────────────────────────────────────────────────

/** GET /api/system/dict/type/list — 字典类型分页列表 */
export function getDictTypeList(params: DictTypeQuery) {
  return get<PagedResult<DictTypeResponse>>('/system/dict/type/list', params)
}

/** GET /api/system/dict/type/{id} — 字典类型详情 */
export function getDictTypeById(id: number) {
  return get<DictTypeResponse>(`/system/dict/type/${id}`)
}

/** POST /api/system/dict/type — 新增字典类型 */
export function createDictType(data: CreateDictTypeRequest) {
  return post<DictTypeResponse>('/system/dict/type', data)
}

/** PUT /api/system/dict/type — 修改字典类型 */
export function updateDictType(data: UpdateDictTypeRequest) {
  return put<DictTypeResponse>('/system/dict/type', data)
}

/** DELETE /api/system/dict/type/{ids} — 删除字典类型（批量，逗号分隔） */
export function deleteDictType(ids: string) {
  return del<null>(`/system/dict/type/${ids}`)
}

// ─── 字典数据 ────────────────────────────────────────────────────

/** GET /api/system/dict/data/type/{dictType} — 按字典类型查数据（下拉选项使用） */
export function getDictDataByType(dictType: string) {
  return get<DictDataResponse[]>(`/system/dict/data/type/${dictType}`)
}

/** GET /api/system/dict/data/list — 字典数据分页列表 */
export function getDictDataList(params: DictDataQuery) {
  return get<PagedResult<DictDataResponse>>('/system/dict/data/list', params)
}

/** POST /api/system/dict/data — 新增字典数据 */
export function createDictData(data: CreateDictDataRequest) {
  return post<DictDataResponse>('/system/dict/data', data)
}

/** PUT /api/system/dict/data — 修改字典数据 */
export function updateDictData(data: UpdateDictDataRequest) {
  return put<DictDataResponse>('/system/dict/data', data)
}

/** DELETE /api/system/dict/data/{ids} — 删除字典数据（批量，逗号分隔） */
export function deleteDictData(ids: string) {
  return del<null>(`/system/dict/data/${ids}`)
}
