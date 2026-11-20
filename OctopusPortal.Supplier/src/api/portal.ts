// ── 供应商门户 API（真实优先 + mock 降级） ──
// 身份绑定与后端端点就绪后（2026-11-20），所有方法 LIVE=1 时经网关调用真实 API，
// 失败自动降级 mock（保证页面不崩）。后端 /api/portal/** 已按 sub→供应商做"仅本人"过滤。
// LIVE=0（默认）保持 mock 行为。

import { LIVE, http, unwrap } from './http'
import {
  mockSummary, mockQuotes, mockOrders, mockQualifications,
  mockStatementSummary, mockStatementItems,
} from './mock'
import type {
  PortalSummary, PurchaseOrder, Qualification, QuoteRequest, StatementItem, StatementSummary,
} from './types'

async function live<T>(method: string, call: () => Promise<T>, fallbackValue: T): Promise<T> {
  try {
    return await call()
  } catch (e) {
    console.warn(`[portal:supp] ${method} 真实 API 失败，降级 mock：`, e)
    return fallbackValue
  }
}

// 后端 MesPurchaseOrder DTO → 门户 PurchaseOrder
type BackendPurchase = {
  purchaseId: number; purchaseCode: string; supplierId: number
  status: string; totalAmount: number; createdAt: string
  expectedDate?: string; items?: Array<{ productName: string; quantity: number; unit?: string }>
}
function mapOrder(b: BackendPurchase): PurchaseOrder {
  const first = b.items?.[0]
  const statusMap: Record<string, PurchaseOrder['status']> = {
    approved: 'preparing', confirmed: 'preparing', producing: 'producing',
    ready: 'ready', shipped: 'shipped', completed: 'completed',
  }
  return {
    id: String(b.purchaseId),
    code: b.purchaseCode,
    productName: first?.productName ?? `采购单 ${b.purchaseCode}`,
    quantity: first?.quantity ?? 0,
    unit: first?.unit ?? '—',
    amount: b.totalAmount,
    status: statusMap[b.status] ?? 'preparing',
    planDeliveryDate: b.expectedDate?.slice(0, 10) ?? '—',
    createdAt: b.createdAt?.slice(0, 10) ?? '',
  }
}

// 后端 /api/portal/* 字段已对齐门户契约，映射仅做防御性兜底
type BackendQuote = {
  id: string; code: string; productName: string; sku: string; quantity: number; unit: string
  deadline: string; status: string
  myQuote?: { unitPrice: number; deliveryDays: number; remark?: string; quotedAt: string; status: string }
}
function mapQuote(b: BackendQuote): QuoteRequest {
  return {
    id: b.id, code: b.code, productName: b.productName, sku: b.sku,
    quantity: b.quantity, unit: b.unit, deadline: b.deadline,
    status: (['pending', 'responded', 'closed'].includes(b.status) ? b.status : 'pending') as QuoteRequest['status'],
    myQuote: b.myQuote ? {
      unitPrice: b.myQuote.unitPrice, deliveryDays: b.myQuote.deliveryDays,
      remark: b.myQuote.remark, quotedAt: b.myQuote.quotedAt,
      status: (['under_review', 'accepted', 'rejected'].includes(b.myQuote.status)
        ? b.myQuote.status : 'under_review') as NonNullable<QuoteRequest['myQuote']>['status'],
    } : undefined,
  }
}

type ShipmentResult = { orderCode: string; trackingNo: string; carrier: string; shippedAt: string }

export const portalApi = {
  // ✅ 真实端点：GET /api/portal/summary
  async getSummary(): Promise<PortalSummary> {
    if (!LIVE) return mockSummary
    return live('getSummary', () => unwrap<PortalSummary>(http.get('/api/portal/summary')), mockSummary)
  },

  // ✅ 真实端点：GET /api/portal/quotes
  async getQuoteRequests(): Promise<QuoteRequest[]> {
    if (!LIVE) return mockQuotes
    return live('getQuoteRequests', async () => {
      const rows = await unwrap<BackendQuote[]>(http.get('/api/portal/quotes'))
      return rows.map(mapQuote)
    }, mockQuotes)
  },

  // ✅ 真实端点：POST /api/portal/quotes/{id}/submit
  async submitQuote(
    quoteId: string,
    input: { unitPrice: number; deliveryDays: number; remark?: string }
  ): Promise<QuoteRequest> {
    if (!LIVE) {
      const target = mockQuotes.find((q) => q.id === quoteId)
      if (!target) throw new Error('询价单不存在')
      const now = new Date()
      const pad = (n: number) => String(n).padStart(2, '0')
      target.status = 'responded'
      target.myQuote = {
        unitPrice: input.unitPrice, deliveryDays: input.deliveryDays, remark: input.remark,
        quotedAt: `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())} ${pad(now.getHours())}:${pad(now.getMinutes())}`,
        status: 'under_review',
      }
      return target
    }
    return live('submitQuote', async () => {
      const row = await unwrap<BackendQuote>(http.post(`/api/portal/quotes/${quoteId}/submit`, {
        unitPrice: input.unitPrice, deliveryDays: input.deliveryDays, remark: input.remark,
      }))
      return mapQuote(row)
    }, (() => {
      const target = mockQuotes.find((q) => q.id === quoteId)
      if (!target) throw new Error('询价单不存在')
      return { ...target, status: 'responded' as const }
    })())
  },

  // ✅ 真实端点：GET /api/purchase（MES，后端按 sub 强制本人供应商）
  async getOrders(): Promise<PurchaseOrder[]> {
    if (!LIVE) return mockOrders
    return live('getOrders', async () => {
      const rows = await unwrap<BackendPurchase[]>(http.get('/api/purchase'))
      return rows.map(mapOrder)
    }, mockOrders)
  },

  // ✅ 真实端点：POST /api/portal/shipments
  async registerShipment(
    orderId: string,
    input: { trackingNo: string; carrier: string }
  ): Promise<ShipmentResult> {
    if (!LIVE) {
      const order = mockOrders.find((o) => o.id === orderId)
      if (!order) throw new Error('采购订单不存在')
      order.status = 'shipped'
      return { orderCode: order.code, trackingNo: input.trackingNo, carrier: input.carrier, shippedAt: new Date().toISOString() }
    }
    return live('registerShipment', async () => {
      // 后端按采购单数字 id 定位，前端 orderId 为字符串
      return unwrap<ShipmentResult>(http.post('/api/portal/shipments', {
        orderId: Number(orderId), trackingNo: input.trackingNo, carrier: input.carrier,
      }))
    }, { orderCode: `PO-${orderId}`, trackingNo: input.trackingNo, carrier: input.carrier, shippedAt: new Date().toISOString() })
  },

  // ✅ 真实端点：GET /api/portal/statement/summary
  async getStatementSummary(): Promise<StatementSummary> {
    if (!LIVE) return mockStatementSummary
    return live('getStatementSummary',
      () => unwrap<StatementSummary>(http.get('/api/portal/statement/summary')), mockStatementSummary)
  },
  // ✅ 真实端点：GET /api/portal/statement/items
  async getStatementItems(): Promise<StatementItem[]> {
    if (!LIVE) return mockStatementItems
    return live('getStatementItems',
      () => unwrap<StatementItem[]>(http.get('/api/portal/statement/items')), mockStatementItems)
  },
  // ✅ 真实端点：GET /api/portal/qualifications
  async getQualifications(): Promise<Qualification[]> {
    if (!LIVE) return mockQualifications
    return live('getQualifications',
      () => unwrap<Qualification[]>(http.get('/api/portal/qualifications')), mockQualifications)
  },
  // ✅ 真实端点：POST /api/portal/qualifications
  async uploadQualification(input: { title: string; category: string; fileName: string }): Promise<Qualification> {
    const fallbackVal: Qualification = {
      id: String(Date.now()), title: input.title, category: input.category,
      fileName: input.fileName, fileSize: '—', status: 'pending',
      uploadedAt: new Date().toISOString().slice(0, 10),
    }
    if (!LIVE) return fallbackVal
    return live('uploadQualification',
      () => unwrap<Qualification>(http.post('/api/portal/qualifications', input)), fallbackVal)
  },
}
