import { createRouter, createWebHistory } from 'vue-router'
import { getUser, login } from '@/services/oidcService'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/workspaces' },
    {
      path: '/callback',
      name: 'callback',
      component: () => import('@/views/CallbackView.vue'),
      meta: { public: true },
    },
    {
      // 身份服务不可达等基础设施故障的落地页。
      // 没有它的话，守卫里抛出的异常会让首屏什么也不渲染（表现为纯白页面）。
      path: '/error',
      name: 'error',
      component: () => import('@/views/ErrorView.vue'),
      meta: { public: true },
    },
    {
      path: '/workspaces',
      name: 'workspaces',
      component: () => import('@/views/WorkspacesView.vue'),
    },
    {
      path: '/invites',
      name: 'invites',
      component: () => import('@/views/InvitesView.vue'),
    },
    {
      // 组织成员管理：禁用/改密/移出（零售用户不走 UMC，必须有这个入口）
      path: '/members',
      name: 'members',
      component: () => import('@/views/MembersView.vue'),
    },
    {
      // 被邀请人的落地页（长 token 形式）：必须匿名可达
      path: '/invite/accept',
      name: 'invite-accept',
      component: () => import('@/views/InviteAcceptView.vue'),
      meta: { public: true },
    },
    {
      // 手动输入邀请码：/join
      // 适用于邀请链接被聊天工具/浏览器改写协议（http→https）而打不开的情况
      path: '/join',
      name: 'join',
      component: () => import('@/views/JoinView.vue'),
      meta: { public: true },
    },
    {
      // 短链接形式：/i/B1V2d9xK —— 推荐发给别人用这个，短且可口头传达
      path: '/i/:code',
      name: 'invite-short',
      component: () => import('@/views/InviteAcceptView.vue'),
      meta: { public: true },
    },
  ],
})

function errorRoute(err: unknown, target: string) {
  const msg = err instanceof Error ? err.message : String(err)
  return { name: 'error', query: { msg, target } }
}

router.beforeEach(async (to) => {
  if (to.meta.public) return true

  try {
    const user = await getUser()
    if (!user || user.expired) {
      // 会跳转到 Keycloak；Keycloak 不可达时该调用抛异常
      await login(to.fullPath)
      return false
    }
    return true
  } catch (err) {
    // 关键：导航守卫抛异常会导致 vue-router 中止导航，
    // 首屏没有任何组件被渲染 → 用户看到纯白页面且无从判断原因。
    // 这里统一兜到 /error，至少把「连不上身份服务」讲清楚。
    console.error('[router] 认证前置检查失败', err)
    return errorRoute(err, to.fullPath)
  }
})

// 兜底：任何导航期异常都不要再变成白屏
router.onError((err, to) => {
  console.error('[router] 导航失败', err)
  const target = to?.fullPath ?? '/workspaces'
  if (target.startsWith('/error')) return
  void router.replace(errorRoute(err, target))
})

export default router
