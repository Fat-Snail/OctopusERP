const Layout = () => import("@/layout/index.vue");

export default {
  path: "/plm",
  name: "PLM",
  component: Layout,
  redirect: "/plm/product",
  meta: {
    icon: "ep/goods",
    title: "商品管理",
    rank: 1
  },
  children: [
    {
      path: "/plm/product",
      name: "ProductList",
      component: () => import("@/views/product/list/index.vue"),
      meta: {
        title: "商品列表",
        showLink: true
      }
    },
    {
      path: "/plm/category/manage",
      name: "CategoryManage",
      component: () => import("@/views/category/manage/index.vue"),
      meta: {
        title: "类目管理",
        showLink: true
      }
    },
    {
      path: "/plm/category-model",
      name: "CategoryModel",
      component: () => import("@/views/category/model/index.vue"),
      meta: {
        title: "类目模型",
        showLink: true
      }
    },
    {
      path: "/plm/product/create",
      name: "ProductCreate",
      component: () => import("@/views/product/create/index.vue"),
      meta: {
        title: "新增商品",
        showLink: false
      }
    },
    {
      path: "/plm/product/edit/:id",
      name: "ProductEdit",
      component: () => import("@/views/product/create/index.vue"),
      meta: {
        title: "编辑商品",
        showLink: false
      }
    }
  ]
} satisfies RouteConfigsTable;
