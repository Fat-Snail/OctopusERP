export interface GarmentTeam {
  teamId: number
  teamCode: string
  teamName: string
  groupLabel: string
  leader: string
  leaderUmcUserId: number | null
  umcDeptId: number | null
  isOutsource: boolean
  outsourcePartnerName: string | null
  headcount: number | null
  status: string
  sortOrder: number
}

export interface GarmentScheduleCell {
  scheduleId: number
  teamId: number
  customer: string | null
  customerId: number | null
  orderNo: string | null
  contractId: number | null
  styleCode: string | null
  plmProductId: number | null
  oaApprovalId: number | null
  workOrderId: number | null
  scheduleStatus: string
  dueDate: string | null
  plannedQty: number | null
  sewingQty: number | null
  cuttingQty: number | null
  totalHours: number | null
  headcount: number | null
  avgWage: number | null
  fabricStatus: string | null
  remark: string | null
  /** 是否已归档（早于 archiveCutoff），归档记录只读不可修改/删除 */
  archived?: boolean
}

export interface GarmentCalendar {
  teams: Pick<GarmentTeam, 'teamId' | 'teamCode' | 'teamName' | 'groupLabel' | 'leader' | 'leaderUmcUserId' | 'umcDeptId' | 'isOutsource' | 'outsourcePartnerName'>[]
  dates: string[]
  cells: Record<string, GarmentScheduleCell>
  /** 归档截止日 yyyy-MM-dd，早于此日期为归档（只读） */
  archiveCutoff?: string
}

export interface GarmentArchiveSummary {
  archiveCutoff: string
  totalArchived: number
  years: Array<{
    year: number
    total: number
    seasons: string[]
    months: Array<{ month: number; count: number }>
  }>
}

export interface GarmentScheduleListRow extends GarmentScheduleCell {
  scheduleDate: string
  season: string
  teamCode: string
  teamName: string
  leader: string
}

export interface GarmentMonthlySummaryRow {
  teamId: number
  teamName: string
  yearMonth: string
  plannedQty: number
  sewingQty: number
  cuttingQty: number
  achievementRate: number
}

export interface GarmentScheduleQuery {
  keyword?: string
  season?: string
  from?: string
  to?: string
  teamId?: number
}

export interface CandidateLeader {
  umcUserId: number
  userName: string
  nickName: string | null
  email: string | null
  isOccupied: boolean
}

export interface UpsertGarmentTeamRequest {
  teamId?: number
  teamCode: string
  teamName: string
  groupLabel?: string
  leader?: string
  leaderUmcUserId?: number | null
  umcDeptId?: number | null
  isOutsource: boolean
  outsourcePartnerName?: string | null
  headcount?: number | null
  status?: string
  sortOrder?: number
  remark?: string | null
}
