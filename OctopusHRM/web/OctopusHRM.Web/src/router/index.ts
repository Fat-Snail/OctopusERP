import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { oidcService } from '@/services/oidcService'

const routes: RouteRecordRaw[] = [
  { path: '/login', name: 'Login', component: () => import('@/views/LoginView.vue'), meta: { requiresAuth: false } },
  { path: '/callback', name: 'Callback', component: () => import('@/views/CallbackView.vue'), meta: { requiresAuth: false } },
  {
    path: '/', component: () => import('@/layouts/AppLayout.vue'), meta: { requiresAuth: true }, redirect: '/home',
    children: [
      {
        path: 'home',
        name: 'Home',
        component: () => import('@/views/HomeView.vue'),
        meta: { title: '人力概览' }
      },
      {
        path: 'employees',
        name: 'Employees',
        component: () => import('@/views/EmployeesView.vue'),
        meta: { title: '员工档案' }
      },
      {
        path: 'payroll',
        name: 'Payroll',
        component: () => import('@/views/PayrollView.vue'),
        meta: { title: '薪资核算' }
      },
      {
        path: 'leave-kpi',
        name: 'LeaveKpi',
        component: () => import('@/views/LeaveKpiView.vue'),
        meta: { title: '假期与绩效' }
      },
    ]
  },
  { path: '/:pathMatch(.*)*', redirect: '/home' }
]

const router = createRouter({ history: createWebHistory(), routes })

router.beforeEach((to, _from, next) => {
  if (to.meta['requiresAuth'] === false) {
    if (oidcService.isLoggedIn() && to.path === '/login') next('/home')
    else next()
  } else if (!oidcService.isLoggedIn()) {
    next('/login')
  } else next()
})

export default router
