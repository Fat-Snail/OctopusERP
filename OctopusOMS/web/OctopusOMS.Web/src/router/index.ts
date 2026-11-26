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
        meta: { title: '订单概览' }
      },
      {
        path: 'channels',
        name: 'Channels',
        component: () => import('@/views/ChannelsView.vue'),
        meta: { title: '渠道与店铺' }
      },
      {
        path: 'orders',
        name: 'Orders',
        component: () => import('@/views/OrdersView.vue'),
        meta: { title: '订单管理' }
      },
      {
        path: 'mappings',
        name: 'Mappings',
        component: () => import('@/views/MappingsView.vue'),
        meta: { title: 'SKU 映射' }
      },
      {
        path: 'after-sales',
        name: 'AfterSales',
        component: () => import('@/views/AfterSalesView.vue'),
        meta: { title: '售后管理' }
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
