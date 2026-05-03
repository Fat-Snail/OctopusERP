import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useUserStore } from '@/store/modules/user'
import { useTabsStore } from '@/store/modules/tabs'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/login/index.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/',
    component: () => import('@/layouts/index.vue'),
    meta: { requiresAuth: true },
    redirect: '/dashboard/workbench',
    children: [
      {
        path: 'dashboard/workbench',
        name: 'Workbench',
        component: () => import('@/views/dashboard/workbench/index.vue'),
        meta: { title: '工作台首页' }
      },
      {
        path: 'dashboard/analysis',
        name: 'Analysis',
        component: () => import('@/views/dashboard/analysis/index.vue'),
        meta: { title: '数据分析' }
      },
      {
        path: 'dashboard/statistics',
        name: 'Statistics',
        component: () => import('@/views/dashboard/statistics/index.vue'),
        meta: { title: '统计报表' }
      },
      {
        path: 'system/user',
        name: 'SystemUser',
        component: () => import('@/views/system/user/index.vue'),
        meta: { title: '用户管理' }
      },
      {
        path: 'system/dept',
        name: 'SystemDept',
        component: () => import('@/views/system/dept/index.vue'),
        meta: { title: '机构管理' }
      },
      {
        path: 'system/post',
        name: 'SystemPost',
        component: () => import('@/views/system/post/index.vue'),
        meta: { title: '职位管理' }
      },
      {
        path: 'system/menu',
        name: 'SystemMenu',
        component: () => import('@/views/system/menu/index.vue'),
        meta: { title: '菜单管理' }
      },
      {
        path: 'system/role',
        name: 'SystemRole',
        component: () => import('@/views/system/role/index.vue'),
        meta: { title: '角色管理' }
      },
      {
        path: 'system/dict',
        name: 'SystemDict',
        component: () => import('@/views/system/dict/index.vue'),
        meta: { title: '字典管理' }
      },
      {
        path: 'monitor/online',
        name: 'MonitorOnline',
        component: () => import('@/views/monitor/online/index.vue'),
        meta: { title: '在线用户' }
      },
      {
        path: 'monitor/server',
        name: 'MonitorServer',
        component: () => import('@/views/monitor/server/index.vue'),
        meta: { title: '服务监控' }
      },
      {
        path: 'monitor/operlog',
        name: 'MonitorOperlog',
        component: () => import('@/views/monitor/operlog/index.vue'),
        meta: { title: '操作日志' }
      },
      {
        path: 'monitor/logininfor',
        name: 'MonitorLogininfor',
        component: () => import('@/views/monitor/logininfor/index.vue'),
        meta: { title: '访问日志' }
      },
      {
        path: 'tool/notice',
        name: 'ToolNotice',
        component: () => import('@/views/tool/notice/index.vue'),
        meta: { title: '公告管理' }
      },
      {
        path: 'tool/file',
        name: 'ToolFile',
        component: () => import('@/views/tool/file/index.vue'),
        meta: { title: '文件管理' }
      },
      {
        path: 'tool/job',
        name: 'ToolJob',
        component: () => import('@/views/tool/job/index.vue'),
        meta: { title: '任务调度' }
      },
      {
        path: 'tool/config',
        name: 'ToolConfig',
        component: () => import('@/views/tool/config/index.vue'),
        meta: { title: '系统配置' }
      },
      {
        path: 'tool/mail',
        name: 'ToolMail',
        component: () => import('@/views/tool/mail/index.vue'),
        meta: { title: '邮件短信' }
      },
      {
        path: 'tool/client',
        name: 'ToolClient',
        component: () => import('@/views/tool/client/index.vue'),
        meta: { title: '接入应用' }
      },
      {
        path: 'profile',
        name: 'Profile',
        component: () => import('@/views/profile/index.vue'),
        meta: { title: '个人中心' }
      }
    ]
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/dashboard/workbench'
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to, _from) => {
  const userStore = useUserStore()
  if (to.meta['requiresAuth'] === false) {
    if (userStore.isLoggedIn && to.path === '/login') return '/dashboard/workbench'
    return true
  }
  if (!userStore.isLoggedIn) return '/login'
  return true
})

router.afterEach((to) => {
  const title = to.meta['title'] as string | undefined
  if (title && to.meta['requiresAuth'] !== false) {
    const tabsStore = useTabsStore()
    tabsStore.openTab(to.path, title)
  }
})

export default router
