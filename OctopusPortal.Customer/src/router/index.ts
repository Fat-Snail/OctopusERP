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
        { path: 'inquiries', component: () => import('@/views/inquiries/index.vue') },
        { path: 'inquiries/new', component: () => import('@/views/inquiries/new.vue') },
        { path: 'contracts', component: () => import('@/views/contracts/index.vue') },
        { path: 'contracts/:id', component: () => import('@/views/contracts/detail.vue') },
        { path: 'logistics', component: () => import('@/views/logistics/index.vue') },
        { path: 'statements', component: () => import('@/views/statements/index.vue') },
        { path: 'support', component: () => import('@/views/support/index.vue') },
        { path: 'documents', component: () => import('@/views/documents/index.vue') },
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
