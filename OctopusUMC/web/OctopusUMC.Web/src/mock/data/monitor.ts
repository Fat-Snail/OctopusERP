import type { OnlineUserResponse, OperlogResponse, LoginfoResponse, ServerInfo } from '@/api/monitor/types'

export const onlineUsersData: OnlineUserResponse[] = [
  {
    tokenId: 'tok-001',
    userId: 1,
    userName: 'admin',
    deptName: '章鱼科技集团',
    ipaddr: '192.168.1.1',
    loginLocation: '内网IP',
    browser: 'Chrome 120',
    os: 'Windows 10',
    loginTime: '2024-04-01 09:00:00'
  },
  {
    tokenId: 'tok-002',
    userId: 2,
    userName: 'zhang_san',
    deptName: '前端组',
    ipaddr: '192.168.1.2',
    loginLocation: '北京市',
    browser: 'Firefox 119',
    os: 'macOS 14',
    loginTime: '2024-04-01 09:30:00'
  },
  {
    tokenId: 'tok-003',
    userId: 3,
    userName: 'li_si',
    deptName: '后端组',
    ipaddr: '192.168.1.3',
    loginLocation: '上海市',
    browser: 'Edge 120',
    os: 'Windows 11',
    loginTime: '2024-04-01 10:00:00'
  }
]

export const operlogsData: OperlogResponse[] = [
  {
    operId: 1,
    title: '用户管理',
    operName: 'admin',
    operUrl: '/api/system/user',
    requestMethod: 'POST',
    operParam: '{"userName":"test","nickName":"测试"}',
    jsonResult: '{"code":200,"msg":"操作成功"}',
    status: 0,
    errorMsg: null,
    operTime: '2024-04-01 09:05:00',
    costTime: 120
  },
  {
    operId: 2,
    title: '角色管理',
    operName: 'admin',
    operUrl: '/api/system/role',
    requestMethod: 'GET',
    operParam: '{"pageNum":1,"pageSize":10}',
    jsonResult: '{"code":200,"msg":"查询成功"}',
    status: 0,
    errorMsg: null,
    operTime: '2024-04-01 09:10:00',
    costTime: 56
  },
  {
    operId: 3,
    title: '用户登录',
    operName: 'zhang_san',
    operUrl: '/api/account/login',
    requestMethod: 'POST',
    operParam: '{"userName":"zhang_san"}',
    jsonResult: '{"code":200,"msg":"登录成功"}',
    status: 0,
    errorMsg: null,
    operTime: '2024-04-01 09:30:00',
    costTime: 234
  },
  {
    operId: 4,
    title: '字典管理',
    operName: 'li_si',
    operUrl: '/api/system/dict/type/1',
    requestMethod: 'DELETE',
    operParam: '{"dictId":1}',
    jsonResult: '{"code":500,"msg":"系统内置字典不能删除"}',
    status: 1,
    errorMsg: '系统内置字典不能删除',
    operTime: '2024-04-01 10:15:00',
    costTime: 89
  },
  {
    operId: 5,
    title: '菜单管理',
    operName: 'admin',
    operUrl: '/api/system/menu',
    requestMethod: 'PUT',
    operParam: '{"menuId":1,"menuName":"工作台"}',
    jsonResult: '{"code":200,"msg":"修改成功"}',
    status: 0,
    errorMsg: null,
    operTime: '2024-04-01 11:00:00',
    costTime: 78
  }
]

export const loginInfosData: LoginfoResponse[] = [
  {
    infoId: 1,
    userName: 'admin',
    ipaddr: '192.168.1.1',
    loginLocation: '内网IP',
    browser: 'Chrome 120',
    os: 'Windows 10',
    status: 0,
    msg: '登录成功',
    loginTime: '2024-04-01 09:00:00'
  },
  {
    infoId: 2,
    userName: 'zhang_san',
    ipaddr: '192.168.1.2',
    loginLocation: '北京市',
    browser: 'Firefox 119',
    os: 'macOS 14',
    status: 0,
    msg: '登录成功',
    loginTime: '2024-04-01 09:30:00'
  },
  {
    infoId: 3,
    userName: 'unknown',
    ipaddr: '10.0.0.99',
    loginLocation: '未知位置',
    browser: 'Chrome 118',
    os: 'Linux',
    status: 1,
    msg: '用户名或密码错误',
    loginTime: '2024-04-01 08:45:00'
  },
  {
    infoId: 4,
    userName: 'li_si',
    ipaddr: '192.168.1.3',
    loginLocation: '上海市',
    browser: 'Edge 120',
    os: 'Windows 11',
    status: 0,
    msg: '登录成功',
    loginTime: '2024-04-01 10:00:00'
  },
  {
    infoId: 5,
    userName: 'wang_wu',
    ipaddr: '192.168.1.5',
    loginLocation: '广州市',
    browser: 'Safari 17',
    os: 'macOS 13',
    status: 1,
    msg: '账号已停用',
    loginTime: '2024-04-01 11:30:00'
  }
]

export const serverInfoData: ServerInfo = {
  cpu: { cpuNum: 4, used: 35, sys: 10, free: 55 },
  mem: { total: 16384, used: 8397, free: 7987 },
  jvm: { total: 512, used: 256, free: 256, version: '.NET 8.0.0' },
  sysFiles: [
    {
      dirName: 'C:\\',
      sysTypeName: 'NTFS',
      typeName: '本地磁盘',
      total: '256 GB',
      free: '128 GB',
      used: '128 GB',
      usage: 50
    },
    {
      dirName: 'D:\\',
      sysTypeName: 'NTFS',
      typeName: '本地磁盘',
      total: '512 GB',
      free: '300 GB',
      used: '212 GB',
      usage: 41.4
    }
  ]
}
