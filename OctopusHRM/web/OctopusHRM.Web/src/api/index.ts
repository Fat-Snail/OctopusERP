import type { ApiResponse } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

// ── 员工 ──────────────────────────────────────────────────────
export interface Employee {
  employeeId: number
  employeeNo: string
  fullName: string
  department: string | null
  position: string | null
  employmentType: string
  hiredAt: string | null
  regularizedAt: string | null
  resignedAt: string | null
  status: string
  baseSalary: number | null
  phone: string | null
  email: string | null
}

export async function getEmployees(params?: { status?: string; keyword?: string }): Promise<Employee[]> {
  return unwrap(await http.get<ApiResponse<Employee[]>>('/employee', { params }))
}

export async function getEmployee(id: number): Promise<Employee> {
  return unwrap(await http.get<ApiResponse<Employee>>(`/employee/${id}`))
}

export async function createEmployee(data: Partial<Employee> & { employeeNo: string; fullName: string }): Promise<Employee> {
  return unwrap(await http.post<ApiResponse<Employee>>('/employee', data))
}

export async function resignEmployee(id: number, resignedAt: string): Promise<Employee> {
  return unwrap(await http.put<ApiResponse<Employee>>(`/employee/${id}/resign`, { resignedAt }))
}

// ── 薪资核算 ──────────────────────────────────────────────────
export interface PayrollRun {
  payrollRunId: number
  periodCode: string
  year: number
  month: number
  status: string
  employeeCount: number
  totalGrossPay: number
  totalNetPay: number
  finVoucherId: number | null
  createdAt: string
  paidAt: string | null
}

export interface PayrollDetail {
  detailId: number
  employeeId: number
  employeeNo: string
  employeeName: string
  attendanceDays: number
  leaveDays: number
  grossPay: number
  deductions: number
  netPay: number
  remark: string | null
}

export interface PayrollRunDetail extends PayrollRun {
  details?: PayrollDetail[]
}

export async function getPayrollRuns(): Promise<PayrollRun[]> {
  return unwrap(await http.get<ApiResponse<PayrollRun[]>>('/payroll'))
}

export async function getPayrollRun(id: number): Promise<PayrollRunDetail> {
  return unwrap(await http.get<ApiResponse<PayrollRunDetail>>(`/payroll/${id}`))
}

export async function startPayroll(year: number, month: number): Promise<PayrollRun> {
  return unwrap(await http.post<ApiResponse<PayrollRun>>('/payroll/start', { year, month }))
}

export async function calculatePayroll(id: number): Promise<PayrollRun> {
  return unwrap(await http.put<ApiResponse<PayrollRun>>(`/payroll/${id}/calculate`))
}

export async function approvePayroll(id: number): Promise<PayrollRun> {
  return unwrap(await http.put<ApiResponse<PayrollRun>>(`/payroll/${id}/approve-pay`))
}

// ── 假期与 KPI ────────────────────────────────────────────────
export interface LeaveBalance {
  balanceId: number
  employeeId: number
  leaveType: string
  year: number
  totalDays: number
  usedDays: number
  remainingDays: number
}

export interface Kpi {
  kpiId: number
  employeeId: number
  period: string
  title: string
  score: number | null
  grade: string | null
  comment: string | null
  status: string
  createdAt: string
}

export async function getLeaveBalance(employeeId: number, year?: number): Promise<LeaveBalance[]> {
  return unwrap(await http.get<ApiResponse<LeaveBalance[]>>(`/leave/balance/${employeeId}`, { params: { year } }))
}

export async function adjustLeaveBalance(data: { employeeId: number; leaveType: string; year: number; totalDays: number }): Promise<LeaveBalance> {
  return unwrap(await http.put<ApiResponse<LeaveBalance>>('/leave/balance', data))
}

export async function getKpis(employeeId: number): Promise<Kpi[]> {
  return unwrap(await http.get<ApiResponse<Kpi[]>>(`/kpi/${employeeId}`))
}

export async function createKpi(data: Partial<Kpi> & { employeeId: number; period: string; title: string }): Promise<Kpi> {
  return unwrap(await http.post<ApiResponse<Kpi>>('/kpi', data))
}

export async function submitKpi(id: number, score: number, comment: string): Promise<Kpi> {
  return unwrap(await http.put<ApiResponse<Kpi>>(`/kpi/${id}/submit`, { score, comment }))
}
