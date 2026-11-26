import type { ApiResponse, PagedResult } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

// ── 渠道与店铺 ────────────────────────────────────────────────
export interface Channel {
  channelId: number
  channelCode: string
  channelName: string
  channelType: string
  status: string
  remark: string | null
  createdAt: string
}

export interface Shop {
  shopId: number
  channelId: number
  shopCode: string
  shopName: string
  region: string | null
  currency: string
  status: string
  createdAt: string
  channel?: Channel | null
}

export interface AdapterInfo {
  channelCode: string
  mode: 'live' | 'simulated' | 'sandbox'
  description: string
}

export interface SkuMapping {
  mappingId: number
  channelId: number
  channelSkuCode: string
  plmProductId: number
  note: string | null
  createdAt: string
}

export async function getChannels(): Promise<Channel[]> {
  return unwrap(await http.get<ApiResponse<Channel[]>>('/catalog/channels'))
}

export async function getShops(channelId?: number): Promise<Shop[]> {
  return unwrap(await http.get<ApiResponse<Shop[]>>('/catalog/shops', { params: { channelId } }))
}

export async function getAdapters(): Promise<AdapterInfo[]> {
  return unwrap(await http.get<ApiResponse<AdapterInfo[]>>('/channel-sync/adapters'))
}

export async function getMappings(channelId?: number): Promise<SkuMapping[]> {
  return unwrap(await http.get<ApiResponse<SkuMapping[]>>('/catalog/sku-mappings', { params: { channelId } }))
}

export async function createMapping(data: { channelId: number; channelSkuCode: string; plmProductId: number; note?: string }): Promise<SkuMapping> {
  return unwrap(await http.post<ApiResponse<SkuMapping>>('/catalog/sku-mappings', data))
}

export interface SyncOutcome {
  fetched: number
  created: number
  skipped: number
}

export async function syncOrders(shopId: number, since?: string): Promise<SyncOutcome> {
  return unwrap(await http.post<ApiResponse<SyncOutcome>>(`/channel-sync/sync/${shopId}`, null, { params: { since } }))
}

export async function pushShipment(orderId: number, trackingNo: string, carrier: string): Promise<unknown> {
  return unwrap(await http.post<ApiResponse<unknown>>(`/channel-sync/shipment/${orderId}`, { trackingNo, carrier }))
}

export async function pushStock(shopId: number, items: { channelSkuCode: string; quantity: number }[]): Promise<unknown> {
  return unwrap(await http.post<ApiResponse<unknown>>(`/channel-sync/stock/${shopId}`, { items }))
}

// ── 订单 ──────────────────────────────────────────────────────
export interface OrderItem {
  itemId: number
  channelSkuCode: string
  productName: string
  spec: string | null
  quantity: number
  unitPrice: number
  amount: number
}

export interface Order {
  orderId: number
  orderNo: string
  shopId: number
  externalOrderNo: string
  customerName: string
  totalAmount: number
  paidAmount: number
  currency: string
  status: string
  orderDate: string
  shippedAt: string | null
  remark: string | null
  shop?: Shop | null
  items?: OrderItem[]
}

export async function getOrders(params: { shopId?: number; status?: string; pageNum: number; pageSize: number }): Promise<PagedResult<Order>> {
  return unwrap(await http.get<ApiResponse<PagedResult<Order>>>('/order', { params }))
}

export async function getOrder(id: number): Promise<Order> {
  return unwrap(await http.get<ApiResponse<Order>>(`/order/${id}`))
}

export async function payOrder(id: number, amount: number): Promise<Order> {
  return unwrap(await http.put<ApiResponse<Order>>(`/order/${id}/pay`, { amount }))
}

export async function pushWms(id: number): Promise<Order> {
  return unwrap(await http.put<ApiResponse<Order>>(`/order/${id}/push-wms`))
}

export async function cancelOrder(id: number, reason: string): Promise<Order> {
  return unwrap(await http.put<ApiResponse<Order>>(`/order/${id}/cancel`, { reason }))
}

// ── 售后 ──────────────────────────────────────────────────────
export interface AfterSale {
  afterSaleId: number
  afterSaleNo: string
  orderId: number
  afterSaleType: string
  amount: number
  reason: string
  status: string
  customerNote: string | null
  operatorNote: string | null
  createdAt: string
}

export async function getAfterSales(status?: string): Promise<AfterSale[]> {
  return unwrap(await http.get<ApiResponse<AfterSale[]>>('/after-sale', { params: { status } }))
}

export async function createAfterSale(data: { orderId: number; type: string; amount: number; reason?: string; note?: string }): Promise<AfterSale> {
  return unwrap(await http.post<ApiResponse<AfterSale>>('/after-sale', data))
}

export async function approveAfterSale(id: number, note: string): Promise<AfterSale> {
  return unwrap(await http.put<ApiResponse<AfterSale>>(`/after-sale/${id}/approve`, { note }))
}

export async function completeAfterSale(id: number): Promise<AfterSale> {
  return unwrap(await http.put<ApiResponse<AfterSale>>(`/after-sale/${id}/complete`))
}
