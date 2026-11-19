// ── 客户门户 API（真实优先 + mock 降级） ──
// 有后端端点的方法：LIVE 开启时经网关调用真实 API，失败自动降级 mock（保证页面不崩）。
// 无后端端点的方法（物流/对账/客服/文档/汇总）：保持 mock，并显式 warn 标注缺口。
// 替换 mock.ts 的门户入口；视图只需把 import 从 '@/api/mock' 改为 '@/api/portal'。

import { LIVE, http, unwrap } from './http'
import {
  mockSummary, mockInquiries, mockContracts, mockShipments,
  mockStatementSummary, mockStatementItems, mockChat, mockDocuments,
} from './mock'
import type {
  ChatMessage, Contract, DocumentItem, Inquiry, PortalSummary,
  Shipment, StatementItem, StatementSummary,
} from './types'

/** 无真实端点时的统一降级日志，便于识别哪些数据仍是 mock。 */
function fallback(method: string, reason: string): void {
  console.warn(`[portal:cust] ${method} 使用 mock 降级：${reason}`)
}

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

export const portalApi = {
  // 汇总：后端无聚合端点 → mock（warn）
  async getSummary(): Promise<PortalSummary> {
    fallback('getSummary', '后端无门户汇总端点 /api/portal/summary')
    return mockSummary
  },

  // ✅ 真实端点：GET /api/inquiry（网关 /gw/crm）
  async getInquiries(): Promise<Inquiry[]> {
    if (!LIVE) return mockInquiries
    return live('getInquiries', async () => {
      const rows = await unwrap<BackendInquiry[]>(http.get('/api/inquiry'))
      return rows.map(mapInquiry)
    }, mockInquiries)
  },

  // ✅ 真实端点：POST /api/inquiry（契约不同，需按后端 CreateInquiryRequest 组装）
  async createInquiry(input: { productName: string; quantity: number; unit: string; remark?: string }): Promise<Inquiry> {
    if (!LIVE) {
      // 保留原 mock 创建逻辑
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
      // 后端需 customerId；门户客户身份绑定尚未落地，暂以标题承载产品信息（详见 TODO）
      const created = await unwrap<BackendInquiry>(http.post('/api/inquiry', {
        customerId: 0,
        title: input.productName,
        description: [input.quantity + input.unit, input.remark].filter(Boolean).join(' / '),
      }))
      return mapInquiry(created)
    } catch (e) {
      console.warn('[portal:cust] createInquiry 真实 API 失败，降级 mock：', e)
      throw e // 创建是写操作，失败必须让用户知道，不做静默 mock 成功
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

  async getShipments(): Promise<Shipment[]> {
    fallback('getShipments', '后端无客户物流轨迹端点（WMS 侧待建）')
    return mockShipments
  },
  async getStatementSummary(): Promise<StatementSummary> {
    fallback('getStatementSummary', '后端无客户对账端点（FIN 侧待建）')
    return mockStatementSummary
  },
  async getStatementItems(): Promise<StatementItem[]> {
    fallback('getStatementItems', '后端无客户对账端点（FIN 侧待建）')
    return mockStatementItems
  },
  async getChatHistory(): Promise<ChatMessage[]> {
    fallback('getChatHistory', '后端无在线客服端点')
    return mockChat
  },
  async sendChatMessage(text: string): Promise<ChatMessage> {
    fallback('sendChatMessage', '后端无在线客服端点')
    const now = new Date()
    const pad = (n: number) => String(n).padStart(2, '0')
    return { id: String(Date.now()), from: 'me', text, time: `${pad(now.getHours())}:${pad(now.getMinutes())}` }
  },
  async getDocuments(): Promise<DocumentItem[]> {
    fallback('getDocuments', '后端无文档库端点')
    return mockDocuments
  },
}
