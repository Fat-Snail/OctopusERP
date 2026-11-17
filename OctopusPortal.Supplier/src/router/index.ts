import { createRouter, createWebHistory } from 'vue-router'
import { oidcService } from '@/services/oidcService'
import { useUserStore } from '@/store/user'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      component: () => import('@/views/login/index.vue'),
      meta: { public: true },
    },
    {
      path: '/callback',
      component: () => import('@/views/callback/index.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      component: () => import('@/layouts/H5Layout.vue'),
      children: [
        { path: '', redirect: '/home' },
        { path: 'home', component: () => import('@/views/home/index.vue') },
        { path: 'quotes', component: () => import('@/views/quotes/index.vue') },
        { path: 'quotes/respond/:id', component: () => import('@/views/quotes/respond.vue') },
        { path: 'orders', component: () => import('@/views/orders/index.vue') },
        { path: 'orders/ship/:id', component: () => import('@/views/orders/ship.vue') },
        { path: 'statements', component: () => import('@/views/statements/index.vue') },
        { path: 'qualifications', component: () => import('@/views/qualifications/index.vue') },
        { path: 'profile', component: () => import('@/views/profile/index.vue') },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/home' },
  ],
})

router.beforeEach((to) => {
  if (to.meta['public']) return true

  if (!oidcService.isLoggedIn()) {
    return '/login'
  }

  const userStore = useUserStore()
  if (!userStore.userName) {
    userStore.loadFromOidc()
  }

  return true
})

export default router
