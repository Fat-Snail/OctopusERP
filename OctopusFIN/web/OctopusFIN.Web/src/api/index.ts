import type { ApiResponse, PagedResult } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

// ── 凭证 ──────────────────────────────────────────────────────
export interface VoucherEntry {
  entryId: number
  accountId: number
  accountCode: string
  accountName: string
  summary: string
  debitAmount: number
  creditAmount: number
}

export interface Voucher {
  voucherId: number
  voucherNo: string
  periodId: number
  voucherDate: string
  voucherType: string
  summary: string
  totalDebit: number
  totalCredit: number
  status: string
  source: string
  postedAt: string | null
  entries?: VoucherEntry[]
}

export interface VoucherEntryInput {
  accountId: number
  summary?: string
  debitAmount: number
  creditAmount: number
  currency?: string
}

export async function getVouchers(params: { periodId?: number; status?: string; source?: string; pageNum: number; pageSize: number }): Promise<PagedResult<Voucher>> {
  return unwrap(await http.get<ApiResponse<PagedResult<Voucher>>>('/voucher', { params }))
}

export async function getVoucher(id: number): Promise<Voucher> {
  return unwrap(await http.get<ApiResponse<Voucher>>(`/voucher/${id}`))
}

export async function createVoucher(data: {
  periodId?: number
  voucherDate?: string
  voucherType?: string
  summary?: string
  source?: string
  sourceRef?: string
  entries: VoucherEntryInput[]
}): Promise<Voucher> {
  return unwrap(await http.post<ApiResponse<Voucher>>('/voucher', data))
}

export async function postVoucher(id: number): Promise<Voucher> {
  return unwrap(await http.put<ApiResponse<Voucher>>(`/voucher/${id}/post`))
}

export async function voidVoucher(id: number): Promise<Voucher> {
  return unwrap(await http.put<ApiResponse<Voucher>>(`/voucher/${id}/void`))
}

// ── 科目 ──────────────────────────────────────────────────────
export interface Account {
  accountId: number
  accountCode: string
  accountName: string
  accountType: string
  direction: string
  parentId: number | null
  level: number
  isLeaf: boolean
  status: string
  remark: string | null
}

export async function getAccounts(params?: { type?: string; keyword?: string }): Promise<Account[]> {
  return unwrap(await http.get<ApiResponse<Account[]>>('/account', { params }))
}

export async function createAccount(data: Partial<Account> & { accountCode: string; accountName: string }): Promise<Account> {
  return unwrap(await http.post<ApiResponse<Account>>('/account', data))
}

export async function updateAccount(id: number, data: { accountName?: string; remark?: string; status?: string }): Promise<Account> {
  return unwrap(await http.put<ApiResponse<Account>>(`/account/${id}`, data))
}

// ── 会计期间 ──────────────────────────────────────────────────
export interface Period {
  periodId: number
  periodCode: string
  year: number
  month: number
  status: string
  closedAt: string | null
}

export async function getPeriods(): Promise<Period[]> {
  return unwrap(await http.get<ApiResponse<Period[]>>('/period'))
}

export async function getCurrentPeriod(): Promise<Period> {
  return unwrap(await http.get<ApiResponse<Period>>('/period/current'))
}

export async function closePeriod(id: number): Promise<Period> {
  return unwrap(await http.put<ApiResponse<Period>>(`/period/${id}/close`))
}

export async function reopenPeriod(id: number): Promise<Period> {
  return unwrap(await http.put<ApiResponse<Period>>(`/period/${id}/reopen`))
}

// ── 应收应付 ──────────────────────────────────────────────────
export interface ArInvoice {
  invoiceId: number
  invoiceNo: string
  customerName: string
  amount: number
  receivedAmount: number
  currency: string
  invoiceDate: string
  dueDate: string | null
  status: string
}

export interface ApInvoice {
  invoiceId: number
  invoiceNo: string
  supplierName: string
  amount: number
  paidAmount: number
  currency: string
  invoiceDate: string
  dueDate: string | null
  status: string
}

export async function getArList(status?: string): Promise<ArInvoice[]> {
  return unwrap(await http.get<ApiResponse<ArInvoice[]>>('/ar', { params: { status } }))
}

export async function registerReceipt(id: number, data: { amount: number; paymentMethod?: string; bankReference?: string }): Promise<unknown> {
  return unwrap(await http.post<ApiResponse<unknown>>(`/ar/${id}/receipt`, data))
}

export async function getApList(status?: string): Promise<ApInvoice[]> {
  return unwrap(await http.get<ApiResponse<ApInvoice[]>>('/ap', { params: { status } }))
}

export async function registerPayment(id: number, data: { amount: number; paymentMethod?: string; bankReference?: string }): Promise<unknown> {
  return unwrap(await http.post<ApiResponse<unknown>>(`/ap/${id}/payment`, data))
}

// ── 报表 ──────────────────────────────────────────────────────
export interface TrialBalanceRow {
  accountCode: string
  accountName: string
  direction: string
  debitTotal: number
  creditTotal: number
  balance: number
}

export interface TrialBalanceResponse {
  rows: TrialBalanceRow[]
  totalDebit: number
  totalCredit: number
  isBalanced: boolean
}

export interface MonthEndClosingReport {
  periodId: number
  periodCode: string
  periodStatus: string
  closedAt: string | null
  closedBy: number | null
  voucherTotal: number
  postedCount: number
  draftCount: number
  voidCount: number
  postedDebit: number
  postedCredit: number
  revenueTotal: number
  expenseTotal: number
  netProfit: number
  closingVoucherNo: string | null
  closingAmount: number | null
  balanceSnapshot: TrialBalanceRow[]
  trialBalanceOk: boolean
}

export async function getTrialBalance(periodId?: number): Promise<TrialBalanceResponse> {
  return unwrap(await http.get<ApiResponse<TrialBalanceResponse>>('/reports/trial-balance', { params: { periodId } }))
}

export async function getMonthEndClosing(periodId: number): Promise<MonthEndClosingReport> {
  return unwrap(await http.get<ApiResponse<MonthEndClosingReport>>(`/reports/month-end-closing/${periodId}`))
}
