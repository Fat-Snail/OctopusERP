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
        { path: 'approval', component: () => import('@/views/approval/index.vue') },
        { path: 'approval/detail/:id', component: () => import('@/views/approval/detail/index.vue') },
        { path: 'attendance', component: () => import('@/views/attendance/index.vue') },
        { path: 'notice', component: () => import('@/views/notice/index.vue') },
        { path: 'notice/:id', component: () => import('@/views/notice/detail/index.vue') },
        { path: 'contact', component: () => import('@/views/contact/index.vue') },
        { path: 'meeting', component: () => import('@/views/meeting/index.vue') },
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
