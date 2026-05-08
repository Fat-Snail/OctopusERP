/**
 * PLM routes using the new AppShell layout (shadcn-vue + Tailwind v4 design system).
 * These routes are served under /plm/* and share the new module rail + sub menu.
 */
export default {
  path: '/plm-shell',
  name: 'PlmShell',
  component: () => import('@/layouts/plm.vue'),
  redirect: '/plm-shell/product',
  meta: {
    title: 'PLM',
    showLink: false,
    rank: 99,
  },
  children: [
    {
      path: '/plm-shell/product',
      name: 'PlmProductNew',
      component: () => import('@/views/product/list/index.vue'),
      meta: {
        title: '商品列表',
        showLink: true,
      },
    },
    {
      path: '/plm-shell/product/create',
      name: 'PlmProductCreate',
      component: () => import('@/views/product/form/index.vue'),
      meta: {
        title: '新建商品',
        showLink: false,
      },
    },
    {
      path: '/plm-shell/product/edit/:id',
      name: 'PlmProductEdit',
      component: () => import('@/views/product/form/index.vue'),
      meta: {
        title: '编辑商品',
        showLink: false,
      },
    },
    {
      path: '/plm-shell/category',
      name: 'PlmCategory',
      component: () => import('@/views/category/model/index.vue'),
      meta: {
        title: '商品分类',
        showLink: true,
      },
    },
  ],
} satisfies RouteConfigsTable
