import { createRouter, createWebHistory } from 'vue-router'
import { useUserStore } from '@/store/user'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'Login',
      component: () => import('@/views/login/index.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/',
      component: () => import('@/layouts/H5Layout.vue'),
      meta: { requiresAuth: true },
      redirect: '/home',
      children: [
        {
          path: 'home',
          name: 'Home',
          component: () => import('@/views/home/index.vue'),
          meta: { title: '首页' }
        },
        {
          path: 'profile',
          name: 'Profile',
          component: () => import('@/views/profile/index.vue'),
          meta: { title: '我的' }
        }
      ]
    },
    {
      path: '/profile/pwd',
      name: 'ProfilePwd',
      component: () => import('@/views/profile/pwd/index.vue'),
      meta: { requiresAuth: true }
    },
    { path: '/:pathMatch(.*)*', redirect: '/home' }
  ]
})

router.beforeEach(async (to) => {
  if (to.meta['requiresAuth'] === false) return true
  const store = useUserStore()
  if (!store.isLoggedIn) {
    await store.fetchMe()
    if (!store.isLoggedIn) return '/login'
  }
  return true
})

export default router
