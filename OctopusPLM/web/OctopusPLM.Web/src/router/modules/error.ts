// 错误页保留为可路由跳转的页面，但不在侧边栏展示（参考 OA 的精简菜单）
export default {
  path: "/error",
  redirect: "/error/403",
  meta: {
    icon: "ri/information-line",
    title: "异常页面",
    rank: 9,
    showLink: false
  },
  children: [
    {
      path: "/error/403",
      name: "403",
      component: () => import("@/views/error/403.vue"),
      meta: {
        title: "403"
      }
    },
    {
      path: "/error/404",
      name: "404",
      component: () => import("@/views/error/404.vue"),
      meta: {
        title: "404"
      }
    },
    {
      path: "/error/500",
      name: "500",
      component: () => import("@/views/error/500.vue"),
      meta: {
        title: "500"
      }
    }
  ]
} satisfies RouteConfigsTable;
