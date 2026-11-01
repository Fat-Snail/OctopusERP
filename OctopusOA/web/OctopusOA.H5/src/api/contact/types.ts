export interface ContactDeptNode {
  deptId: number
  deptName: string
  parentId: number
  orderNum: number
  userCount: number
  children: ContactDeptNode[]
}

export interface ContactUserDept {
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
  depts: ContactUserDept[]
}
