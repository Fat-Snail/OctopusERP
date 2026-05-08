export interface OnlineUserResponse {
  tokenId: string
  userId: number
  userName: string
  deptName: string
  ipaddr: string
  loginLocation: string
  browser: string
  os: string
  loginTime: string
}

export interface OperlogResponse {
  operId: number
  title: string
  operName: string
  operUrl: string
  requestMethod: string
  operParam: string
  jsonResult: string
  status: 0 | 1
  errorMsg: string | null
  operTime: string
  costTime: number
}

export interface LoginfoResponse {
  infoId: number
  userName: string
  ipaddr: string
  loginLocation: string
  browser: string
  os: string
  status: 0 | 1
  msg: string
  loginTime: string
}

export interface DashboardSummary {
  onlineUserCount: number
  todayLoginCount: number
  noticeCount: number
  totalUserCount: number
}

export interface ServerInfo {
  cpu: { cpuNum: number; used: number; sys: number; free: number }
  mem: { total: number; used: number; free: number }
  jvm: { total: number; used: number; free: number; version: string }
  sysFiles: Array<{
    dirName: string
    sysTypeName: string
    typeName: string
    total: string
    free: string
    used: string
    usage: number
  }>
}
