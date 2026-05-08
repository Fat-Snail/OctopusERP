import type { ApiResponse } from '../types'
import type {
  ChannelDef,
  ChannelCategoryMappingItem,
  CreateChannelRequest,
  UpdateChannelRequest,
  CreateChannelCategoryMappingRequest,
  UpdateChannelCategoryMappingRequest,
  CreateChannelAttributeMappingRequest,
  UpdateChannelAttributeMappingRequest,
} from './types'
import { createAuthedHttp } from '@/utils/plmHttp'

const http = createAuthedHttp('/api')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

// ── Channel ───────────────────────────────────────────────────────────────────

export async function getChannelList(): Promise<ChannelDef[]> {
  return unwrap(await http.get<ApiResponse<ChannelDef[]>>('/channel'))
}

export async function createChannel(data: CreateChannelRequest): Promise<{ channelId: number }> {
  return unwrap(await http.post<ApiResponse<{ channelId: number }>>('/channel', data))
}

export async function updateChannel(id: number, data: UpdateChannelRequest): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/channel/${id}`, data))
}

export async function deleteChannel(id: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/channel/${id}`))
}

// ── Category Mapping ──────────────────────────────────────────────────────────

export async function getChannelMappings(channelId: number): Promise<ChannelCategoryMappingItem[]> {
  return unwrap(await http.get<ApiResponse<ChannelCategoryMappingItem[]>>(`/channel/${channelId}/mappings`))
}

export async function createChannelMapping(channelId: number, data: CreateChannelCategoryMappingRequest): Promise<{ mappingId: number }> {
  return unwrap(await http.post<ApiResponse<{ mappingId: number }>>(`/channel/${channelId}/mappings`, data))
}

export async function updateChannelMapping(channelId: number, mappingId: number, data: UpdateChannelCategoryMappingRequest): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/channel/${channelId}/mappings/${mappingId}`, data))
}

export async function deleteChannelMapping(channelId: number, mappingId: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/channel/${channelId}/mappings/${mappingId}`))
}

// ── Attribute Mapping ─────────────────────────────────────────────────────────

export async function createAttrMapping(channelId: number, mappingId: number, data: CreateChannelAttributeMappingRequest): Promise<{ mappingId: number }> {
  return unwrap(await http.post<ApiResponse<{ mappingId: number }>>(`/channel/${channelId}/mappings/${mappingId}/attributes`, data))
}

export async function updateAttrMapping(channelId: number, mappingId: number, attrMappingId: number, data: UpdateChannelAttributeMappingRequest): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/channel/${channelId}/mappings/${mappingId}/attributes/${attrMappingId}`, data))
}

export async function deleteAttrMapping(channelId: number, mappingId: number, attrMappingId: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/channel/${channelId}/mappings/${mappingId}/attributes/${attrMappingId}`))
}
