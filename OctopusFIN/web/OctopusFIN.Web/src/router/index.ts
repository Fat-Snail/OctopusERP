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
        meta: { title: '财务概览' }
      },
      {
        path: 'vouchers',
        name: 'Vouchers',
        component: () => import('@/views/VouchersView.vue'),
        meta: { title: '凭证管理' }
      },
      {
        path: 'accounts',
        name: 'Accounts',
        component: () => import('@/views/AccountsView.vue'),
        meta: { title: '会计科目' }
      },
      {
        path: 'ar-ap',
        name: 'ArAp',
        component: () => import('@/views/ArApView.vue'),
        meta: { title: '应收应付' }
      },
      {
        path: 'periods',
        name: 'Periods',
        component: () => import('@/views/PeriodsView.vue'),
        meta: { title: '会计期间' }
      },
      {
        path: 'reports',
        name: 'Reports',
        component: () => import('@/views/ReportsView.vue'),
        meta: { title: '财务报表' }
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
