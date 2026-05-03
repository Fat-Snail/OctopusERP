export interface ContactDeptNode {
  deptId: number
  parentId: number
  deptName: string
  orderNum: number
  userCount: number
  children: ContactDeptNode[]
}

export interface ContactDeptInfo {
  deptId: number
  deptName: string
  postName: string | null
  isPrimary: boolean
}

export interface ContactUser {
  umcUserId: number
  userName: string
  nickName: string
  email: string
  phoneNumber: string | null
  avatar: string | null
  status: number
  depts: ContactDeptInfo[]
}
