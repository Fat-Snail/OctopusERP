import type { PostResponse } from '@/api/system/types'

export const postsData: PostResponse[] = [
  {
    postId: 1,
    postName: '董事长',
    postCode: 'chairman',
    postSort: 1,
    status: 1,
    createTime: '2024-01-01 00:00:00',
    remark: null
  },
  {
    postId: 2,
    postName: '总经理',
    postCode: 'general_manager',
    postSort: 2,
    status: 1,
    createTime: '2024-01-01 00:00:00',
    remark: null
  },
  {
    postId: 3,
    postName: '技术总监',
    postCode: 'cto',
    postSort: 3,
    status: 1,
    createTime: '2024-01-01 00:00:00',
    remark: '负责技术方向'
  },
  {
    postId: 4,
    postName: '工程师',
    postCode: 'engineer',
    postSort: 4,
    status: 1,
    createTime: '2024-01-01 00:00:00',
    remark: null
  }
]
