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
        meta: { title: '生产看板' }
      },
      {
        path: 'supplier',
        name: 'SupplierList',
        component: () => import('@/views/supplier/SupplierListView.vue'),
        meta: { title: '供应商管理' }
      },
      {
        path: 'purchase',
        name: 'PurchaseOrderList',
        component: () => import('@/views/purchase/PurchaseOrderListView.vue'),
        meta: { title: '采购订单' }
      },
      {
        path: 'workorder',
        name: 'WorkOrderList',
        component: () => import('@/views/workorder/WorkOrderListView.vue'),
        meta: { title: '工单管理' }
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
