import { http, HttpResponse } from 'msw'
import type {
  NoticeResponse, JobResponse, ConfigResponse, FileResponse, OidcClientResponse
} from '@/api/system/types'

let notices: NoticeResponse[] = [
  {
    noticeId: 1,
    noticeTitle: '系统维护通知',
    noticeType: '1',
    noticeContent: '本周六凌晨2点将进行系统维护，请做好准备。',
    status: 1,
    createBy: 'admin',
    createTime: '2024-04-01 09:00:00',
    remark: null
  },
  {
    noticeId: 2,
    noticeTitle: '新版本发布公告',
    noticeType: '2',
    noticeContent: 'OctopusUMC v2.0 正式发布，包含多项功能优化。',
    status: 1,
    createBy: 'admin',
    createTime: '2024-04-02 10:00:00',
    remark: null
  },
  {
    noticeId: 3,
    noticeTitle: '安全策略更新通知',
    noticeType: '1',
    noticeContent: '密码策略已更新，请各用户修改密码。',
    status: 0,
    createBy: 'admin',
    createTime: '2024-04-03 14:00:00',
    remark: null
  }
]
let nextNoticeId = notices.length + 1

let jobs: JobResponse[] = [
  {
    jobId: 1,
    jobName: '清理过期Token',
    jobGroup: 'DEFAULT',
    invokeTarget: 'TokenCleanTask.Execute',
    cronExpression: '0 0 2 * * ?',
    status: 1,
    createTime: '2024-01-01 00:00:00',
    remark: '每天凌晨2点清理过期Token'
  },
  {
    jobId: 2,
    jobName: '数据备份',
    jobGroup: 'DEFAULT',
    invokeTarget: 'BackupTask.Execute',
    cronExpression: '0 30 1 * * ?',
    status: 1,
    createTime: '2024-01-01 00:00:00',
    remark: '每日凌晨1:30备份数据'
  },
  {
    jobId: 3,
    jobName: '发送统计报告',
    jobGroup: 'REPORT',
    invokeTarget: 'ReportTask.Execute',
    cronExpression: '0 0 9 ? * MON',
    status: 0,
    createTime: '2024-02-01 00:00:00',
    remark: '每周一早9点发送统计报告'
  }
]
let nextJobId = jobs.length + 1

let configs: ConfigResponse[] = [
  {
    configId: 1,
    configName: '系统名称',
    configKey: 'sys.name',
    configValue: 'OctopusUMC',
    configType: true,
    createTime: '2024-01-01 00:00:00',
    remark: '系统全局名称'
  },
  {
    configId: 2,
    configName: 'Token有效期(分钟)',
    configKey: 'sys.token.expiry',
    configValue: '60',
    configType: true,
    createTime: '2024-01-01 00:00:00',
    remark: null
  },
  {
    configId: 3,
    configName: '密码最小长度',
    configKey: 'sys.password.min_length',
    configValue: '8',
    configType: false,
    createTime: '2024-01-01 00:00:00',
    remark: null
  }
]
let nextConfigId = configs.length + 1

let files: FileResponse[] = [
  {
    ossId: 1,
    fileName: 'avatar_1.png',
    originalName: 'my_photo.png',
    fileSuffix: 'png',
    url: 'https://placeholder.pics/svg/100/DEDEDE/555555/avatar',
    createTime: '2024-03-01 10:00:00',
    createBy: 'admin',
    service: 'local'
  },
  {
    ossId: 2,
    fileName: 'report_2024Q1.pdf',
    originalName: '2024年Q1报告.pdf',
    fileSuffix: 'pdf',
    url: '/uploads/report_2024Q1.pdf',
    createTime: '2024-04-01 09:00:00',
    createBy: 'zhang_san',
    service: 'local'
  }
]
let nextOssId = files.length + 1

let clients: OidcClientResponse[] = [
  {
    id: 'client-001',
    clientId: 'octopus_oa',
    clientName: 'OctopusOA 办公系统',
    clientType: 'confidential',
    redirectUris: ['http://localhost:5174/callback'],
    postLogoutRedirectUris: ['http://localhost:5174/login'],
    status: 1,
    createdAt: '2024-01-01 00:00:00'
  },
  {
    id: 'client-002',
    clientId: 'mobile_app',
    clientName: '移动端应用',
    clientType: 'public',
    redirectUris: ['octopus://callback'],
    postLogoutRedirectUris: ['octopus://login'],
    status: 1,
    createdAt: '2024-02-01 00:00:00'
  }
]
let nextClientIdx = clients.length + 1

export const toolHandlers = [
  // Notices
  http.get('/api/tool/notice', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const title = url.searchParams.get('noticeTitle') ?? ''
    let filtered = notices
    if (title) filtered = filtered.filter(n => n.noticeTitle.includes(title))
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total: filtered.length } })
  }),
  http.post('/api/tool/notice', async ({ request }) => {
    const body = await request.json() as Omit<NoticeResponse, 'noticeId' | 'createTime' | 'createBy'>
    const n: NoticeResponse = {
      ...body,
      noticeId: nextNoticeId++,
      createBy: 'admin',
      createTime: new Date().toISOString().replace('T', ' ').slice(0, 19)
    }
    notices.push(n)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: n })
  }),
  http.put('/api/tool/notice', async ({ request }) => {
    const body = await request.json() as NoticeResponse
    const idx = notices.findIndex(n => n.noticeId === body.noticeId)
    if (idx !== -1) notices[idx] = body
    return HttpResponse.json({ code: 200, msg: '修改成功', data: body })
  }),
  http.delete('/api/tool/notice/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    notices = notices.filter(n => !ids.includes(n.noticeId))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  }),

  // Jobs
  http.get('/api/tool/job', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const jobName = url.searchParams.get('jobName') ?? ''
    let filtered = jobs
    if (jobName) filtered = filtered.filter(j => j.jobName.includes(jobName))
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total: filtered.length } })
  }),
  http.post('/api/tool/job', async ({ request }) => {
    const body = await request.json() as Omit<JobResponse, 'jobId' | 'createTime'>
    const j: JobResponse = {
      ...body,
      jobId: nextJobId++,
      createTime: new Date().toISOString().replace('T', ' ').slice(0, 19)
    }
    jobs.push(j)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: j })
  }),
  http.put('/api/tool/job', async ({ request }) => {
    const body = await request.json() as JobResponse
    const idx = jobs.findIndex(j => j.jobId === body.jobId)
    if (idx !== -1) jobs[idx] = body
    return HttpResponse.json({ code: 200, msg: '修改成功', data: body })
  }),
  http.delete('/api/tool/job/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    jobs = jobs.filter(j => !ids.includes(j.jobId))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  }),
  http.post('/api/tool/job/run/:id', ({ params }) => {
    return HttpResponse.json({ code: 200, msg: `任务 ${params['id']} 执行成功`, data: null })
  }),

  // Configs
  http.get('/api/tool/config', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const configName = url.searchParams.get('configName') ?? ''
    let filtered = configs
    if (configName) filtered = filtered.filter(c => c.configName.includes(configName))
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total: filtered.length } })
  }),
  http.post('/api/tool/config', async ({ request }) => {
    const body = await request.json() as Omit<ConfigResponse, 'configId' | 'createTime'>
    const c: ConfigResponse = {
      ...body,
      configId: nextConfigId++,
      createTime: new Date().toISOString().replace('T', ' ').slice(0, 19)
    }
    configs.push(c)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: c })
  }),
  http.put('/api/tool/config', async ({ request }) => {
    const body = await request.json() as ConfigResponse
    const idx = configs.findIndex(c => c.configId === body.configId)
    if (idx !== -1) configs[idx] = body
    return HttpResponse.json({ code: 200, msg: '修改成功', data: body })
  }),
  http.delete('/api/tool/config/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    configs = configs.filter(c => !ids.includes(c.configId))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  }),

  // Files
  http.get('/api/tool/file', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const rows = files.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total: files.length } })
  }),
  http.post('/api/tool/file/upload', async () => {
    const f: FileResponse = {
      ossId: nextOssId++,
      fileName: `upload_${Date.now()}.png`,
      originalName: 'uploaded_file.png',
      fileSuffix: 'png',
      url: 'https://placeholder.pics/svg/100',
      createTime: new Date().toISOString().replace('T', ' ').slice(0, 19),
      createBy: 'admin',
      service: 'local'
    }
    files.push(f)
    return HttpResponse.json({ code: 200, msg: '上传成功', data: f })
  }),
  http.delete('/api/tool/file/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    files = files.filter(f => !ids.includes(f.ossId))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  }),

  // Mail/SMS
  http.post('/api/tool/mail/send', async () => {
    return HttpResponse.json({ code: 200, msg: '邮件发送成功', data: null })
  }),
  http.post('/api/tool/sms/send', async () => {
    return HttpResponse.json({ code: 200, msg: '短信发送成功', data: null })
  }),

  // OIDC Clients
  http.get('/api/tool/client', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const rows = clients.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total: clients.length } })
  }),
  http.post('/api/tool/client', async ({ request }) => {
    const body = await request.json() as Omit<OidcClientResponse, 'id' | 'createdAt'>
    const c: OidcClientResponse = {
      ...body,
      id: `client-${String(nextClientIdx++).padStart(3, '0')}`,
      createdAt: new Date().toISOString().replace('T', ' ').slice(0, 19)
    }
    clients.push(c)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: c })
  }),
  http.put('/api/tool/client/:id', async ({ request, params }) => {
    const body = await request.json() as OidcClientResponse
    const idx = clients.findIndex(c => c.id === params['id'])
    if (idx !== -1) clients[idx] = body
    return HttpResponse.json({ code: 200, msg: '修改成功', data: body })
  }),
  http.delete('/api/tool/client/:id', ({ params }) => {
    clients = clients.filter(c => c.id !== params['id'])
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  })
]
