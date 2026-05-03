import type { DeptResponse } from '@/api/system/types'

export const deptsData: DeptResponse[] = [
  {
    deptId: 1,
    parentId: 0,
    deptName: '章鱼科技集团',
    orderNum: 0,
    status: 1,
    createTime: '2024-01-01 00:00:00',
    children: [
      {
        deptId: 2,
        parentId: 1,
        deptName: '技术部',
        orderNum: 1,
        status: 1,
        createTime: '2024-01-01 00:00:00',
        children: [
          {
            deptId: 5,
            parentId: 2,
            deptName: '前端组',
            orderNum: 1,
            status: 1,
            createTime: '2024-01-01 00:00:00',
            children: []
          },
          {
            deptId: 6,
            parentId: 2,
            deptName: '后端组',
            orderNum: 2,
            status: 1,
            createTime: '2024-01-01 00:00:00',
            children: []
          }
        ]
      },
      {
        deptId: 3,
        parentId: 1,
        deptName: '市场部',
        orderNum: 2,
        status: 1,
        createTime: '2024-01-01 00:00:00',
        children: [
          {
            deptId: 7,
            parentId: 3,
            deptName: '品牌推广组',
            orderNum: 1,
            status: 1,
            createTime: '2024-01-01 00:00:00',
            children: []
          }
        ]
      },
      {
        deptId: 4,
        parentId: 1,
        deptName: '行政部',
        orderNum: 3,
        status: 1,
        createTime: '2024-01-01 00:00:00',
        children: [
          {
            deptId: 8,
            parentId: 4,
            deptName: '人力资源组',
            orderNum: 1,
            status: 1,
            createTime: '2024-01-01 00:00:00',
            children: []
          }
        ]
      }
    ]
  }
]

export function flattenDepts(depts: DeptResponse[]): DeptResponse[] {
  const result: DeptResponse[] = []
  function walk(list: DeptResponse[]) {
    for (const d of list) {
      result.push(d)
      if (d.children?.length) walk(d.children)
    }
  }
  walk(depts)
  return result
}
