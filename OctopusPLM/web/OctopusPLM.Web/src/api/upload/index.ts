import { createAuthedHttp } from '@/utils/plmHttp'
import type { ApiResponse } from '../types'

const http = createAuthedHttp('/api')

export async function uploadImage(file: File): Promise<{ url: string }> {
  const formData = new FormData()
  formData.append('file', file)
  const res = await http.post<ApiResponse<{ url: string }>>('/upload', formData)
  if (res.data.code !== 200) throw new Error(res.data.msg || '上传失败')
  return res.data.data
}
