// ── Mock 数据源（UI 先行阶段） ──
// 签名与未来真实 API 对齐：接入后仅需将本模块替换为 axios 实现（走网关/子系统），页面无需改动。

import type {
  PurchaseOrder,
  PortalSummary,
  Qualification,
  QuoteRequest,
  StatementItem,
  StatementSummary,
} from './types'

const delay = (ms = 200) => new Promise<void>((r) => setTimeout(r, ms))

export const mockSummary: PortalSummary = {
  pendingQuotes: 2,
  activeOrders: 3,
  readyToShip: 1,
  outstandingAmount: 96400,
}

export const mockQuotes: QuoteRequest[] = [
  {
    id: '1',
    code: 'RFQ2026110005',
    productName: '涤纶尼龙面料（210T）',
    sku: 'SKU-FT210-BLK',
    quantity: 12000,
    unit: '米',
    deadline: '2026-11-20 18:00',
    status: 'pending',
  },
  {
    id: '2',
    code: 'RFQ2026110003',
    productName: '针织罗纹袖口（2×2）',
    sku: 'SKU-RB202-WHT',
    quantity: 8000,
    unit: '对',
    deadline: '2026-11-18 18:00',
    status: 'responded',
    myQuote: {
      unitPrice: 2.98,
      deliveryDays: 10,
      remark: '含 13% 增值税，50% 预付款',
      quotedAt: '2026-11-14 16:20',
      status: 'under_review',
    },
  },
  {
    id: '3',
    code: 'RFQ2026100029',
    productName: '全棉卫衣面料（280g）',
    sku: 'SKU-FC280-BLUE',
    quantity: 5000,
    unit: '米',
    deadline: '2026-10-30 18:00',
    status: 'responded',
    myQuote: {
      unitPrice: 28.5,
      deliveryDays: 15,
      quotedAt: '2026-10-26 09:15',
      status: 'accepted',
    },
  },
]

export const mockOrders: PurchaseOrder[] = [
  {
    id: '1',
    code: 'PO2026110009',
    productName: '全棉卫衣面料（280g）× 5000 米',
    quantity: 5000,
    unit: '米',
    amount: 142500,
    status: 'producing',
    planDeliveryDate: '2026-11-28',
    createdAt: '2026-11-05',
  },
  {
    id: '2',
    code: 'PO2026100031',
    productName: '涤纶尼龙面料（210T）× 12000 米',
    quantity: 12000,
    unit: '米',
    amount: 86400,
    status: 'ready',
    planDeliveryDate: '2026-11-19',
    createdAt: '2026-10-30',
  },
  {
    id: '3',
    code: 'PO2026100024',
    productName: '针织罗纹袖口（2×2）× 8000 对',
    quantity: 8000,
    unit: '对',
    amount: 23840,
    status: 'completed',
    planDeliveryDate: '2026-10-28',
    createdAt: '2026-10-12',
  },
]

export const mockQualifications: Qualification[] = [
  {
    id: '1',
    title: '营业执照',
    category: '基础资质',
    fileName: 'business-license-2026.pdf',
    fileSize: '1.2 MB',
    status: 'approved',
    uploadedAt: '2026-08-10',
    validUntil: '2029-08-09',
  },
  {
    id: '2',
    title: 'ISO 9001 质量体系认证',
    category: '质量认证',
    fileName: 'iso9001-cert.pdf',
    fileSize: '0.8 MB',
    status: 'approved',
    uploadedAt: '2026-08-12',
    validUntil: '2027-06-30',
  },
  {
    id: '3',
    title: 'OEKO-TEX 环保认证',
    category: '环保认证',
    fileName: 'oekotex-2026.pdf',
    fileSize: '2.3 MB',
    status: 'pending',
    uploadedAt: '2026-11-12',
  },
]

export const mockStatementSummary: StatementSummary = {
  payable: 252740,
  settled: 156340,
  outstanding: 96400,
}

export const mockStatementItems: StatementItem[] = [
  {
    id: '1',
    date: '2026-11-05',
    code: 'PO2026110009',
    description: '采购订单 PO2026110009 预付款',
    amount: 71250,
    status: 'pending',
  },
  {
    id: '2',
    date: '2026-10-30',
    code: 'PO2026100031',
    description: '采购订单 PO2026100031 全款',
    amount: 86400,
    status: 'pending',
  },
  {
    id: '3',
    date: '2026-10-28',
    code: 'PO2026100024',
    description: '采购订单 PO2026100024 尾款结清',
    amount: 156340,
    status: 'settled',
  },
]

// ── Mock API（签名即未来真实 API 契约） ──

export const portalApi = {
  async getSummary(): Promise<PortalSummary> {
    await delay()
    return mockSummary
  },
  async getQuoteRequests(): Promise<QuoteRequest[]> {
    await delay()
    return mockQuotes
  },
  async submitQuote(
    quoteId: string,
    input: { unitPrice: number; deliveryDays: number; remark?: string }
  ): Promise<QuoteRequest> {
    await delay(400)
    const target = mockQuotes.find((q) => q.id === quoteId)
    if (!target) throw new Error('询价单不存在')
    const now = new Date()
    const pad = (n: number) => String(n).padStart(2, '0')
    target.status = 'responded'
    target.myQuote = {
      unitPrice: input.unitPrice,
      deliveryDays: input.deliveryDays,
      remark: input.remark,
      quotedAt: `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())} ${pad(now.getHours())}:${pad(now.getMinutes())}`,
      status: 'under_review',
    }
    return target
  },
  async getOrders(): Promise<PurchaseOrder[]> {
    await delay()
    return mockOrders
  },
  async registerShipment(
    orderId: string,
    input: { trackingNo: string; carrier: string }
  ): Promise<{ orderCode: string; trackingNo: string; carrier: string; shippedAt: string }> {
    await delay(400)
    const order = mockOrders.find((o) => o.id === orderId)
    if (!order) throw new Error('采购订单不存在')
    order.status = 'shipped'
    const now = new Date()
    const pad = (n: number) => String(n).padStart(2, '0')
    return {
      orderCode: order.code,
      trackingNo: input.trackingNo,
      carrier: input.carrier,
      shippedAt: `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())} ${pad(now.getHours())}:${pad(now.getMinutes())}`,
    }
  },
  async getStatementSummary(): Promise<StatementSummary> {
    await delay()
    return mockStatementSummary
  },
  async getStatementItems(): Promise<StatementItem[]> {
    await delay()
    return mockStatementItems
  },
  async getQualifications(): Promise<Qualification[]> {
    await delay()
    return mockQualifications
  },
  async uploadQualification(
    input: { title: string; category: string; fileName: string }
  ): Promise<Qualification> {
    await delay(500)
    const now = new Date()
    const pad = (n: number) => String(n).padStart(2, '0')
    return {
      id: String(Date.now()),
      title: input.title,
      category: input.category,
      fileName: input.fileName,
      fileSize: '—',
      status: 'pending',
      uploadedAt: `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`,
    }
  },
}
