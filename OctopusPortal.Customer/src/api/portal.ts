// ── 客户门户 API（真实优先 + mock 降级） ──
// 身份绑定与后端端点就绪后（2026-11-20），所有方法 LIVE=1 时经网关调用真实 API，
// 失败自动降级 mock（保证页面不崩）。后端 /api/portal/** 已按 sub→客户做"仅本人"过滤。
// LIVE=0（默认）保持 mock 行为，用于网关未启动/后端未部署的开发场景。

import { LIVE, http, unwrap } from './http'
import {
  mockSummary, mockInquiries, mockContracts, mockShipments,
  mockStatementSummary, mockStatementItems, mockChat, mockDocuments,
} from './mock'
import type {
  ChatMessage, Contract, DocumentItem, Inquiry, PortalSummary,
  Shipment, StatementItem, StatementSummary,
} from './types'

/** 真实调用失败时降级，避免单个接口故障拖垮整页。 */
async function live<T>(method: string, call: () => Promise<T>, fallbackValue: T): Promise<T> {
  try {
    return await call()
  } catch (e) {
    console.warn(`[portal:cust] ${method} 真实 API 失败，降级 mock：`, e)
    return fallbackValue
  }
}

// 后端 DTO → 门户类型的映射（后端字段与门户契约不同，集中在此适配）
type BackendInquiry = {
  inquiryId: number; inquiryCode: string; title: string; description?: string
  status: string; createdAt: string; customerName?: string
  items?: Array<{ productName: string; quantity: number; unit?: string }>
}
function mapInquiry(b: BackendInquiry): Inquiry {
  const first = b.items?.[0]
  const statusMap: Record<string, Inquiry['status']> = {
    open: 'submitted', quoted: 'quoted', won: 'won', lost: 'won',
  }
  return {
    id: String(b.inquiryId),
    code: b.inquiryCode,
    productName: first?.productName ?? b.title,
    sku: '—',
    quantity: first?.quantity ?? 0,
    unit: first?.unit ?? '—',
    status: statusMap[b.status] ?? 'submitted',
    createdAt: b.createdAt?.replace('T', ' ').slice(0, 16) ?? '',
    timeline: [{ label: '询盘提交', time: b.createdAt?.slice(5, 16).replace('T', ' ') ?? '', done: true }],
  }
}

type BackendContract = {
  contractId: number; contractCode: string; title: string; totalAmount: number
  status: string; deliveryDate?: string; signDate?: string; customerName?: string
}
function mapContract(b: BackendContract): Contract {
  return {
    id: String(b.contractId),
    code: b.contractCode,
    productName: b.title,
    amount: b.totalAmount,
    status: b.status === 'completed' ? 'completed' : 'executing',
    deliveryDate: b.deliveryDate?.slice(0, 10) ?? '—',
    signedAt: b.signDate?.slice(0, 10) ?? '—',
  }
}

// 后端 /api/portal/* 返回的字段已对齐门户契约，映射仅做防御性兜底
type BackendShipment = {
  id: string; trackingNo: string; carrier: string; status: string
  contractCode: string; productName: string
  traces: Array<{ time: string; desc: string; location?: string }>
}
function mapShipment(b: BackendShipment): Shipment {
  return {
    id: b.id, trackingNo: b.trackingNo, carrier: b.carrier,
    status: (['preparing', 'shipping', 'delivered'].includes(b.status) ? b.status : 'shipping') as Shipment['status'],
    contractCode: b.contractCode, productName: b.productName,
    traces: b.traces ?? [],
  }
}

export const portalApi = {
  // ✅ 真实端点：GET /api/portal/summary
  async getSummary(): Promise<PortalSummary> {
    if (!LIVE) return mockSummary
    return live('getSummary', () => unwrap<PortalSummary>(http.get('/api/portal/summary')), mockSummary)
  },

  // ✅ 真实端点：GET /api/inquiry（网关 /gw/crm）
  async getInquiries(): Promise<Inquiry[]> {
    if (!LIVE) return mockInquiries
    return live('getInquiries', async () => {
      const rows = await unwrap<BackendInquiry[]>(http.get('/api/inquiry'))
      return rows.map(mapInquiry)
    }, mockInquiries)
  },

  // ✅ 真实端点：POST /api/inquiry（后端按 sub 强制归属本人，customerId 占位会被覆盖）
  async createInquiry(input: { productName: string; quantity: number; unit: string; remark?: string }): Promise<Inquiry> {
    if (!LIVE) {
      const now = new Date()
      const pad = (n: number) => String(n).padStart(2, '0')
      return {
        id: String(Date.now()),
        code: `INQ${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}${pad(now.getHours())}${pad(now.getMinutes())}`,
        productName: input.productName, sku: 'SKU-TO-BE-ASSIGNED',
        quantity: input.quantity, unit: input.unit, status: 'submitted',
        createdAt: `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())} ${pad(now.getHours())}:${pad(now.getMinutes())}`,
        timeline: [
          { label: '询盘提交', time: `${pad(now.getMonth() + 1)}-${pad(now.getDate())} ${pad(now.getHours())}:${pad(now.getMinutes())}`, done: true },
          { label: '销售接单', time: '—', done: false },
          { label: '报价中', time: '—', done: false },
          { label: '报价送达', time: '—', done: false },
        ],
      }
    }
    try {
      const created = await unwrap<BackendInquiry>(http.post('/api/inquiry', {
        customerId: 0, // 占位：后端 PortalIdentityService 会强制覆盖为本人客户
        title: input.productName,
        description: [input.quantity + input.unit, input.remark].filter(Boolean).join(' / '),
      }))
      return mapInquiry(created)
    } catch (e) {
      console.warn('[portal:cust] createInquiry 真实 API 失败：', e)
      throw e // 写操作失败必须让用户知道，不做静默 mock 成功
    }
  },

  // ✅ 真实端点：GET /api/contract
  async getContracts(): Promise<Contract[]> {
    if (!LIVE) return mockContracts
    return live('getContracts', async () => {
      const rows = await unwrap<BackendContract[]>(http.get('/api/contract'))
      return rows.map(mapContract)
    }, mockContracts)
  },

  // ✅ 真实端点：GET /api/portal/shipments
  async getShipments(): Promise<Shipment[]> {
    if (!LIVE) return mockShipments
    return live('getShipments', async () => {
      const rows = await unwrap<BackendShipment[]>(http.get('/api/portal/shipments'))
      return rows.map(mapShipment)
    }, mockShipments)
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
  // ✅ 真实端点：GET /api/portal/chat
  async getChatHistory(): Promise<ChatMessage[]> {
    if (!LIVE) return mockChat
    return live('getChatHistory',
      () => unwrap<ChatMessage[]>(http.get('/api/portal/chat')), mockChat)
  },
  // ✅ 真实端点：POST /api/portal/chat
  async sendChatMessage(text: string): Promise<ChatMessage> {
    if (!LIVE) {
      const now = new Date()
      const pad = (n: number) => String(n).padStart(2, '0')
      return { id: String(Date.now()), from: 'me', text, time: `${pad(now.getHours())}:${pad(now.getMinutes())}` }
    }
    return live('sendChatMessage',
      () => unwrap<ChatMessage>(http.post('/api/portal/chat', { text })),
      { id: String(Date.now()), from: 'me', text, time: new Date().toTimeString().slice(0, 5) })
  },
  // ✅ 真实端点：GET /api/portal/documents
  async getDocuments(): Promise<DocumentItem[]> {
    if (!LIVE) return mockDocuments
    return live('getDocuments',
      () => unwrap<DocumentItem[]>(http.get('/api/portal/documents')), mockDocuments)
  },
}
