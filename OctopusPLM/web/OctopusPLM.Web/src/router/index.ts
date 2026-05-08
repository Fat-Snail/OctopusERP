import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { oidcService } from '@/services/oidcService'
import { useTabsStore } from '@/store/modules/tabs'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/login/index.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/callback',
    name: 'Callback',
    component: () => import('@/views/callback/index.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/',
    component: () => import('@/layouts/index.vue'),
    meta: { requiresAuth: true },
    redirect: '/plm/product',
    children: [
      {
        path: 'plm/product',
        name: 'PlmProduct',
        component: () => import('@/views/product/list/index.vue'),
        meta: { title: '商品列表' }
      },
      {
        path: 'plm/product/image-search',
        name: 'PlmProductImageSearch',
        component: () => import('@/views/product/image-search/index.vue'),
        meta: { title: '以图搜商品' }
      },
      {
        path: 'plm/product/create',
        name: 'PlmProductCreate',
        component: () => import('@/views/product/create/index.vue'),
        meta: { title: '新建商品' }
      },
      {
        path: 'plm/product/edit/:id',
        name: 'PlmProductEdit',
        component: () => import('@/views/product/create/index.vue'),
        meta: { title: '编辑商品' }
      },
      {
        path: 'plm/category',
        name: 'PlmCategory',
        component: () => import('@/views/category/model/index.vue'),
        meta: { title: '类目模型' }
      },
      {
        path: 'plm/category/manage',
        name: 'PlmCategoryManage',
        component: () => import('@/views/category/manage/index.vue'),
        meta: { title: '类目管理' }
      },
      {
        path: 'plm/category-model',
        name: 'PlmCategoryModel',
        component: () => import('@/views/category/model/index.vue'),
        meta: { title: '类目模型' }
      },
      {
        path: 'plm/channel',
        name: 'PlmChannel',
        component: () => import('@/views/channel/index.vue'),
        meta: { title: '渠道映射' }
      },
      {
        path: 'plm/profile',
        name: 'PlmProfile',
        component: () => import('@/views/profile/index.vue'),
        meta: { title: '个人中心' }
      },
      {
        path: 'plm/system/model',
        name: 'PlmSystemModel',
        component: () => import('@/views/system/model/index.vue'),
        meta: { title: '模型管理' }
      }
    ]
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/plm/product'
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to, _from, next) => {
  if (to.meta['requiresAuth'] === false) {
    if (oidcService.isLoggedIn() && to.path === '/login') {
      next('/plm/product')
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

router.afterEach((to) => {
  const title = to.meta['title'] as string | undefined
  if (title && to.meta['requiresAuth'] !== false) {
    const tabsStore = useTabsStore()
    tabsStore.openTab(to.path, title)
  }
})

export default router

// ── Legacy pure-admin compatibility stubs ──
// Old layout hooks import these named exports; stub them so TS doesn't error.
export { router }
export const resetRouter = () => { /* stub */ }
export const constantRoutes: unknown[] = []
export const constantMenus: unknown[] = []
export const remainingPaths: string[] = []
