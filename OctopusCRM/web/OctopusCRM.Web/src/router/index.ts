import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { oidcService } from '@/services/oidcService'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/LoginView.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/callback',
    name: 'Callback',
    component: () => import('@/views/CallbackView.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/',
    component: () => import('@/layouts/AppLayout.vue'),
    meta: { requiresAuth: true },
    redirect: '/home',
    children: [
      {
        path: 'home',
        name: 'Home',
        component: () => import('@/views/HomeView.vue'),
        meta: { title: '工作台' }
      },
      {
        path: 'customer',
        name: 'CustomerList',
        component: () => import('@/views/customer/CustomerListView.vue'),
        meta: { title: '客户管理' }
      },
      {
        path: 'inquiry',
        name: 'InquiryList',
        component: () => import('@/views/inquiry/InquiryListView.vue'),
        meta: { title: '询盘管理' }
      },
      {
        path: 'quote',
        name: 'QuoteList',
        component: () => import('@/views/quote/QuoteListView.vue'),
        meta: { title: '报价管理' }
      },
      {
        path: 'contract',
        name: 'ContractList',
        component: () => import('@/views/contract/ContractListView.vue'),
        meta: { title: '合同管理' }
      },
      {
        path: 'bi',
        name: 'BiDashboard',
        component: () => import('@/views/bi/BiDashboardView.vue'),
        meta: { title: '全链路 BI 看板' }
      },
    ]
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/home'
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to, _from, next) => {
  if (to.meta['requiresAuth'] === false) {
    if (oidcService.isLoggedIn() && to.path === '/login') {
      next('/home')
    } else {
      next()
    }
  } else {
    if (!oidcService.isLoggedIn()) {
      next('/login')
    } else {
      next()
    }
  }
})

export default router
