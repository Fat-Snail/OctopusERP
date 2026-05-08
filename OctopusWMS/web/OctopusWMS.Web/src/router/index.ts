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
        meta: { title: '库存看板' }
      },
      {
        path: 'warehouse',
        name: 'WarehouseList',
        component: () => import('@/views/warehouse/WarehouseListView.vue'),
        meta: { title: '仓库管理' }
      },
      {
        path: 'inventory',
        name: 'Inventory',
        component: () => import('@/views/inventory/InventoryView.vue'),
        meta: { title: '库存查询' }
      },
      {
        path: 'inbound',
        name: 'Inbound',
        component: () => import('@/views/inbound/InboundView.vue'),
        meta: { title: '入库管理' }
      },
      {
        path: 'outbound',
        name: 'Outbound',
        component: () => import('@/views/outbound/OutboundView.vue'),
        meta: { title: '出库管理' }
      },
      {
        path: 'stocktake',
        name: 'Stocktake',
        component: () => import('@/views/stocktake/StocktakeView.vue'),
        meta: { title: '盘点管理' }
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
