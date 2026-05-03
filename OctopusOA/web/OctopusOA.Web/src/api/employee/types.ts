export type EmployeeStatus = 'temp' | 'pending' | 'active' | 'rejected' | 'resigned'

export interface EmployeeEducation {
  id?: number
  school: string
  education: string
  major?: string
  startDate?: string
  endDate?: string
  degree?: string
  isFullTime?: boolean
}

export interface EmployeeWorkHistory {
  id?: number
  company: string
  position: string
  startDate?: string
  endDate?: string
  salary?: string
  leaveReason?: string
  refName?: string
  refPhone?: string
}

export interface EmployeeFamily {
  id?: number
  name: string
  relation: string
  workplace?: string
  phone?: string
}

export interface EmployeeEmergencyContact {
  id?: number
  name: string
  relation: string
  phone: string
}

export interface EmployeeResponse {
  employeeId: number
  status: EmployeeStatus
  name: string
  gender: string
  phone: string
  birthDate: string | null
  email: string | null
  ethnicity: string | null
  applyPosition: string
  applyDeptId: number | null
  applyDeptName: string | null
  education: string | null
  graduateSchool: string | null
  major: string | null
  expectedSalary: string | null
  workYears: number | null
  resumeUrl: string | null
  hrRemark: string | null
  photo: string | null
  idCardNo: string | null
  idCardFrontUrl: string | null
  idCardBackUrl: string | null
  politicalStatus: string | null
  maritalStatus: string | null
  nativePlace: string | null
  currentAddress: string | null
  householdAddress: string | null
  bankName: string | null
  bankAccount: string | null
  bankBranch: string | null
  h5Token: string
  h5Url: string | null
  h5FilledAt: string | null
  umcUserId: number | null
  createdBy: number
  createTime: string
  updateTime: string
  educations: EmployeeEducation[]
  workHistories: EmployeeWorkHistory[]
  families: EmployeeFamily[]
  emergencyContacts: EmployeeEmergencyContact[]
}

export interface CreateEmployeeRequest {
  name: string
  gender: string
  phone: string
  birthDate?: string
  email?: string
  ethnicity?: string
  applyPosition: string
  applyDeptId?: number
  applyDeptName?: string
  education?: string
  graduateSchool?: string
  major?: string
  expectedSalary?: string
  workYears?: number
  resumeUrl?: string
  hrRemark?: string
}

export interface H5OnboardResponse {
  employeeId: number
  name: string
  phone: string
  gender: string
  email: string | null
  birthDate: string | null
  ethnicity: string | null
  applyPosition: string
  applyDeptName: string | null
  status: string
  alreadyFilled: boolean
}

export interface H5SubmitRequest {
  photo?: string
  idCardNo?: string
  idCardFrontUrl?: string
  idCardBackUrl?: string
  politicalStatus?: string
  maritalStatus?: string
  nativePlace?: string
  currentAddress?: string
  householdAddress?: string
  bankName?: string
  bankAccount?: string
  bankBranch?: string
  educations: EmployeeEducation[]
  workHistories: EmployeeWorkHistory[]
  families: EmployeeFamily[]
  emergencyContacts: EmployeeEmergencyContact[]
}
