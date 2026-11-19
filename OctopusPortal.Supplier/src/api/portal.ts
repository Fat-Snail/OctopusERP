// ── 供应商门户 API（真实优先 + mock 降级） ──
// 有后端端点的方法：LIVE 开启时经网关调用真实 API，失败自动降级 mock（保证页面不崩）。
// 无后端端点的方法（询价响应/发货登记/对账/资质/汇总）：保持 mock，并显式 warn 标注缺口。

import { LIVE, http, unwrap } from './http'
import {
  mockSummary, mockQuotes, mockOrders, mockQualifications,
  mockStatementSummary, mockStatementItems,
} from './mock'
import type {
  PortalSummary, PurchaseOrder, Qualification, QuoteRequest, StatementItem, StatementSummary,
} from './types'

function fallback(method: string, reason: string): void {
  console.warn(`[portal:supp] ${method} 使用 mock 降级：${reason}`)
}

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

export const portalApi = {
  async getSummary(): Promise<PortalSummary> {
    fallback('getSummary', '后端无供应商门户汇总端点')
    return mockSummary
  },

  async getQuoteRequests(): Promise<QuoteRequest[]> {
    fallback('getQuoteRequests', '后端无「供应商询价响应」端点（CRM 仅有销售侧询盘）')
    return mockQuotes
  },

  async submitQuote(
    quoteId: string,
    input: { unitPrice: number; deliveryDays: number; remark?: string }
  ): Promise<QuoteRequest> {
    fallback('submitQuote', '后端无供应商报价回写端点')
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
  },

  // ✅ 真实端点：GET /api/purchase（MES，支持 supplierId 过滤；身份绑定待后端补）
  async getOrders(): Promise<PurchaseOrder[]> {
    if (!LIVE) return mockOrders
    return live('getOrders', async () => {
      const rows = await unwrap<BackendPurchase[]>(http.get('/api/purchase'))
      return rows.map(mapOrder)
    }, mockOrders)
  },

  async registerShipment(
    orderId: string,
    input: { trackingNo: string; carrier: string }
  ): Promise<{ orderCode: string; trackingNo: string; carrier: string; shippedAt: string }> {
    fallback('registerShipment', '后端无供应商发货登记端点（内部 ship-confirm 不可供供应商调用）')
    const order = mockOrders.find((o) => o.id === orderId)
    if (!order) throw new Error('采购订单不存在')
    order.status = 'shipped'
    const now = new Date()
    const pad = (n: number) => String(n).padStart(2, '0')
    return {
      orderCode: order.code, trackingNo: input.trackingNo, carrier: input.carrier,
      shippedAt: `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())} ${pad(now.getHours())}:${pad(now.getMinutes())}`,
    }
  },

  async getStatementSummary(): Promise<StatementSummary> {
    fallback('getStatementSummary', '后端无供应商对账端点（FIN /api/ap 是企业应付视角）')
    return mockStatementSummary
  },
  async getStatementItems(): Promise<StatementItem[]> {
    fallback('getStatementItems', '后端无供应商对账端点')
    return mockStatementItems
  },
  async getQualifications(): Promise<Qualification[]> {
    fallback('getQualifications', '后端无资质档案端点')
    return mockQualifications
  },
  async uploadQualification(input: { title: string; category: string; fileName: string }): Promise<Qualification> {
    fallback('uploadQualification', '后端无资质上传端点')
    const now = new Date()
    const pad = (n: number) => String(n).padStart(2, '0')
    return {
      id: String(Date.now()), title: input.title, category: input.category,
      fileName: input.fileName, fileSize: '—', status: 'pending',
      uploadedAt: `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`,
    }
  },
}
