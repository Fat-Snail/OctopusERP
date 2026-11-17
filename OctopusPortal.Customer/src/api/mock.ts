// ── Mock 数据源（UI 先行阶段） ──
// 签名与未来真实 API 对齐：接入后仅需将本模块替换为 axios 实现（走网关/子系统），页面无需改动。

import type {
  ChatMessage,
  Contract,
  DocumentItem,
  Inquiry,
  PortalSummary,
  Shipment,
  StatementItem,
  StatementSummary,
} from './types'

const delay = (ms = 200) => new Promise<void>((r) => setTimeout(r, ms))

export const mockSummary: PortalSummary = {
  openInquiries: 3,
  activeContracts: 2,
  shippingOrders: 1,
  unpaidAmount: 186000,
}

export const mockInquiries: Inquiry[] = [
  {
    id: '1',
    code: 'INQ2026110001',
    productName: '全棉卫衣面料（280g）',
    sku: 'SKU-FC280-BLUE',
    quantity: 5000,
    unit: '米',
    status: 'quoting',
    createdAt: '2026-11-10 09:32',
    timeline: [
      { label: '询盘提交', time: '11-10 09:32', done: true },
      { label: '销售接单', time: '11-10 10:05', done: true },
      { label: '报价中', time: '11-11 14:20', done: false },
      { label: '报价送达', time: '—', done: false },
    ],
  },
  {
    id: '2',
    code: 'INQ2026100087',
    productName: '涤纶尼龙面料（210T）',
    sku: 'SKU-FT210-BLK',
    quantity: 12000,
    unit: '米',
    status: 'quoted',
    quotedAmount: 86400,
    quotedDeliveryDays: 15,
    createdAt: '2026-10-28 15:10',
    timeline: [
      { label: '询盘提交', time: '10-28 15:10', done: true },
      { label: '销售接单', time: '10-28 15:40', done: true },
      { label: '报价中', time: '10-29 09:00', done: true },
      { label: '报价送达', time: '10-30 11:22', done: true },
    ],
  },
  {
    id: '3',
    code: 'INQ2026090042',
    productName: '针织罗纹袖口（2×2）',
    sku: 'SKU-RB202-WHT',
    quantity: 8000,
    unit: '对',
    status: 'won',
    quotedAmount: 23800,
    quotedDeliveryDays: 10,
    createdAt: '2026-09-15 11:05',
    timeline: [
      { label: '询盘提交', time: '09-15 11:05', done: true },
      { label: '报价送达', time: '09-16 16:30', done: true },
      { label: '已成交', time: '09-18 10:00', done: true },
    ],
  },
]

export const mockContracts: Contract[] = [
  {
    id: '1',
    code: 'SO2026110006',
    productName: '针织罗纹袖口（2×2）× 8000 对',
    amount: 23800,
    status: 'executing',
    deliveryDate: '2026-11-30',
    signedAt: '2026-09-18',
  },
  {
    id: '2',
    code: 'SO2026080033',
    productName: '全棉针织T恤坯布 × 20000 米',
    amount: 156000,
    status: 'completed',
    deliveryDate: '2026-10-15',
    signedAt: '2026-08-02',
  },
]

export const mockShipments: Shipment[] = [
  {
    id: '1',
    trackingNo: 'SF1388202611007',
    carrier: '顺丰速运',
    status: 'shipping',
    contractCode: 'SO2026110006',
    productName: '针织罗纹袖口（2×2）',
    traces: [
      { time: '11-15 18:40', desc: '快件已从【杭州转运中心】发出', location: '杭州' },
      { time: '11-15 12:02', desc: '快件已到达【杭州转运中心】', location: '杭州' },
      { time: '11-14 20:15', desc: '仓库已发货，快件交接完成', location: '湖州仓' },
    ],
  },
  {
    id: '2',
    trackingNo: 'YD9988202610002',
    carrier: '韵达快递',
    status: 'delivered',
    contractCode: 'SO2026080033',
    productName: '全棉针织T恤坯布',
    traces: [
      { time: '10-16 14:22', desc: '快件已签收（本人签收）', location: '广州' },
      { time: '10-15 22:10', desc: '快件派送中', location: '广州' },
      { time: '10-13 09:30', desc: '仓库已发货', location: '湖州仓' },
    ],
  },
]

export const mockStatementSummary: StatementSummary = {
  receivable: 179800,
  paid: 156000,
  unpaid: 23800,
}

export const mockStatementItems: StatementItem[] = [
  {
    id: '1',
    date: '2026-11-14',
    type: 'invoice',
    code: 'INV2026110006',
    description: '合同 SO2026110006 发票',
    amount: 23800,
    status: 'pending',
  },
  {
    id: '2',
    date: '2026-10-18',
    type: 'payment',
    code: 'PAY2026100033',
    description: '合同 SO2026080033 尾款',
    amount: 156000,
    status: 'settled',
  },
]

export const mockChat: ChatMessage[] = [
  { id: '1', from: 'agent', text: '您好，我是您的专属客户经理小章，有什么可以帮您？', time: '10:02' },
  { id: '2', from: 'me', text: '请问 INQ2026110001 的报价大概什么时候能出？', time: '10:05' },
  { id: '3', from: 'agent', text: '正在核算 280g 卫衣面料的成本，预计今天 17:00 前给您回复报价单～', time: '10:06' },
]

export const mockDocuments: DocumentItem[] = [
  { id: '1', title: '2026 秋冬面料产品手册', category: '产品手册', size: '8.2 MB', updatedAt: '2026-10-20', downloads: 132 },
  { id: '2', title: '面料洗护与工艺说明', category: '技术文档', size: '1.4 MB', updatedAt: '2026-09-28', downloads: 87 },
  { id: '3', title: 'ODM 合作流程指引', category: '合作指南', size: '2.1 MB', updatedAt: '2026-08-15', downloads: 56 },
]

// ── Mock API（签名即未来真实 API 契约） ──

export const portalApi = {
  async getSummary(): Promise<PortalSummary> {
    await delay()
    return mockSummary
  },
  async getInquiries(): Promise<Inquiry[]> {
    await delay()
    return mockInquiries
  },
  async createInquiry(_input: { productName: string; quantity: number; unit: string; remark?: string }): Promise<Inquiry> {
    await delay(400)
    const now = new Date()
    const pad = (n: number) => String(n).padStart(2, '0')
    return {
      id: String(Date.now()),
      code: `INQ${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}${pad(now.getHours())}${pad(now.getMinutes())}`,
      productName: _input.productName,
      sku: 'SKU-TO-BE-ASSIGNED',
      quantity: _input.quantity,
      unit: _input.unit,
      status: 'submitted',
      createdAt: `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())} ${pad(now.getHours())}:${pad(now.getMinutes())}`,
      timeline: [
        { label: '询盘提交', time: `${pad(now.getMonth() + 1)}-${pad(now.getDate())} ${pad(now.getHours())}:${pad(now.getMinutes())}`, done: true },
        { label: '销售接单', time: '—', done: false },
        { label: '报价中', time: '—', done: false },
        { label: '报价送达', time: '—', done: false },
      ],
    }
  },
  async getContracts(): Promise<Contract[]> {
    await delay()
    return mockContracts
  },
  async getShipments(): Promise<Shipment[]> {
    await delay()
    return mockShipments
  },
  async getStatementSummary(): Promise<StatementSummary> {
    await delay()
    return mockStatementSummary
  },
  async getStatementItems(): Promise<StatementItem[]> {
    await delay()
    return mockStatementItems
  },
  async getChatHistory(): Promise<ChatMessage[]> {
    await delay()
    return mockChat
  },
  async sendChatMessage(text: string): Promise<ChatMessage> {
    await delay(300)
    const now = new Date()
    const pad = (n: number) => String(n).padStart(2, '0')
    return {
      id: String(Date.now()),
      from: 'me',
      text,
      time: `${pad(now.getHours())}:${pad(now.getMinutes())}`,
    }
  },
  async getDocuments(): Promise<DocumentItem[]> {
    await delay()
    return mockDocuments
  },
}
