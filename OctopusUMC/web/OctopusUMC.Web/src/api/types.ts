export interface ApiResponse<T = unknown> {
  code: number;
  msg: string;
  data: T;
}

export interface PagedResult<T> {
  rows: T[];
  total: number;
}

export interface PageQuery {
  pageNum: number;
  pageSize: number;
}

export type Status = 0 | 1;

/** 对应后端 LoginRequest */
export interface LoginRequest {
  userName: string
  password: string
  rememberMe?: boolean
}

/** 对应后端 LoginResponse */
export interface LoginResponse {
  userId: number
  userName: string
  nickName: string
  avatar: string | null
  roles: string[]
  permissions: string[]
  email?: string
  phoneNumber?: string
  sex?: string
  deptName?: string
  postName?: string
}
