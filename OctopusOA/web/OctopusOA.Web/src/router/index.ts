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
    redirect: '/home',
    children: [
      {
        path: 'home',
        name: 'Home',
        component: () => import('@/views/home/index.vue'),
        meta: { title: '首页' }
      },
      {
        path: 'approval/apply',
        name: 'ApprovalApply',
        component: () => import('@/views/approval/apply/index.vue'),
        meta: { title: '发起申请' }
      },
      {
        path: 'approval/mine',
        name: 'ApprovalMine',
        component: () => import('@/views/approval/mine/index.vue'),
        meta: { title: '我的申请' }
      },
      {
        path: 'approval/pending',
        name: 'ApprovalPending',
        component: () => import('@/views/approval/pending/index.vue'),
        meta: { title: '待我审批' }
      },
      {
        path: 'approval/template',
        name: 'ApprovalTemplate',
        component: () => import('@/views/approval/template/index.vue'),
        meta: { title: '流程模板' }
      },
      {
        path: 'approval/all',
        name: 'ApprovalAll',
        component: () => import('@/views/approval/all/index.vue'),
        meta: { title: '全部审批' }
      },
      {
        path: 'attendance/mine',
        name: 'AttendanceMine',
        component: () => import('@/views/attendance/mine/index.vue'),
        meta: { title: '我的考勤' }
      },
      {
        path: 'attendance/stats',
        name: 'AttendanceStats',
        component: () => import('@/views/attendance/stats/index.vue'),
        meta: { title: '考勤统计' }
      },
      {
        path: 'attendance/rule',
        name: 'AttendanceRule',
        component: () => import('@/views/attendance/rule/index.vue'),
        meta: { title: '考勤规则' }
      },
      {
        path: 'attendance/shift',
        name: 'AttendanceShift',
        component: () => import('@/views/attendance/shift/index.vue'),
        meta: { title: '班次管理' }
      },
      {
        path: 'meeting/calendar',
        name: 'MeetingCalendar',
        component: () => import('@/views/meeting/calendar/index.vue'),
        meta: { title: '会议室预订' }
      },
      {
        path: 'meeting/mine',
        name: 'MeetingMine',
        component: () => import('@/views/meeting/mine/index.vue'),
        meta: { title: '我的预订' }
      },
      {
        path: 'meeting/room',
        name: 'MeetingRoom',
        component: () => import('@/views/meeting/room/index.vue'),
        meta: { title: '会议室管理' }
      },
      {
        path: 'notice/list',
        name: 'NoticeList',
        component: () => import('@/views/notice/list/index.vue'),
        meta: { title: '公告中心' }
      },
      {
        path: 'notice/:id',
        name: 'NoticeDetail',
        component: () => import('@/views/notice/detail/index.vue'),
        meta: { title: '公告详情' }
      },
      {
        path: 'contact',
        name: 'Contact',
        component: () => import('@/views/contact/index.vue'),
        meta: { title: '通讯录' }
      },
      {
        path: 'employee/list',
        name: 'EmployeeList',
        component: () => import('@/views/employee/list/index.vue'),
        meta: { title: '职员列表' }
      },
      {
        path: 'employee/:id',
        name: 'EmployeeDetail',
        component: () => import('@/views/employee/detail/index.vue'),
        meta: { title: '档案详情' }
      },
      {
        path: 'admin/user',
        name: 'AdminUser',
        component: () => import('@/views/admin/user/index.vue'),
        meta: { title: '用户管理' }
      },
      {
        path: 'profile',
        name: 'Profile',
        component: () => import('@/views/profile/index.vue'),
        meta: { title: '个人中心' }
      }
    ]
  },
  {
    path: '/h5/onboard/:token',
    name: 'H5Onboard',
    component: () => import('@/views/h5/onboard/index.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/home'
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

let _navCount = 0

router.afterEach((to) => {
  // Sync tab store
  const title = to.meta['title'] as string | undefined
  if (title && to.meta['requiresAuth'] !== false) {
    const tabsStore = useTabsStore()
    tabsStore.openTab(to.path, title)
  }

  // Nuclear cleanup: reset all body styles that reka-ui / useBodyScrollLock may set
  document.body.style.pointerEvents = ''
  document.body.style.overflow = ''
  document.body.style.paddingRight = ''
  document.body.style.marginRight = ''

  // Remove aria-hidden / inert from #app if a stale dialog left them behind
  const appEl = document.getElementById('app')
  if (appEl) {
    if (appEl.hasAttribute('aria-hidden')) appEl.removeAttribute('aria-hidden')
    if (appEl.hasAttribute('inert')) appEl.removeAttribute('inert')
  }

  _navCount++

  if (import.meta.env.DEV) {
    // Run after a microtask so any portal Transitions have finished their final cleanup
    Promise.resolve().then(() => {
      const bodyChildren = Array.from(document.body.children) as HTMLElement[]

      // Non-app children are portals (reka-ui Teleport / toast overlays / confirm overlays)
      const portals = bodyChildren.filter(el => el.id !== 'app')

      // Find any fixed element (across entire doc) that's clickable and not #app subtree
      const allFixed = Array.from(
        document.querySelectorAll('*')
      ).filter(el => {
        const s = window.getComputedStyle(el)
        return (
          (s.position === 'fixed' || s.position === 'sticky') &&
          s.pointerEvents !== 'none' &&
          !document.getElementById('app')?.contains(el)
        )
      }) as HTMLElement[]

      // What element is actually under the cursor at center/common click spots?
      const cx = Math.round(window.innerWidth / 2)
      const cy = Math.round(window.innerHeight / 2)
      const elAtCenter = document.elementFromPoint(cx, cy)
      const elAtTopLeft = document.elementFromPoint(80, 200)   // sidebar area

      const appState = appEl ? {
        ariaHidden: appEl.getAttribute('aria-hidden'),
        inert: appEl.hasAttribute('inert'),
      } : null

      const info = {
        nav: _navCount,
        bodyStyle: document.body.getAttribute('style') || '(empty)',
        appState,
        portalCount: portals.length,
        portals: portals.map(el => ({
          tag: el.tagName,
          id: el.id || '',
          cls: el.className.slice(0, 80),
          style: el.getAttribute('style') ?? '',
          dataState: el.dataset['state'] ?? '',
          childCount: el.children.length,
        })),
        fixedOutsideApp: allFixed.map(el => ({
          tag: el.tagName,
          cls: el.className.slice(0, 80),
          style: el.getAttribute('style') ?? '',
          dataState: el.dataset?.['state'] ?? '',
          rect: (() => { const r = el.getBoundingClientRect(); return `${r.left},${r.top} ${r.width}x${r.height}` })(),
        })),
        elAtCenter: elAtCenter ? `<${elAtCenter.tagName.toLowerCase()} class="${(elAtCenter as HTMLElement).className?.slice(0, 60)}" id="${(elAtCenter as HTMLElement).id}">` : 'null',
        elAtTopLeft: elAtTopLeft ? `<${elAtTopLeft.tagName.toLowerCase()} class="${(elAtTopLeft as HTMLElement).className?.slice(0, 60)}" id="${(elAtTopLeft as HTMLElement).id}">` : 'null',
      }

      const hasIssue = portals.length > 1 || allFixed.length > 0 || appState?.ariaHidden || appState?.inert
      if (hasIssue) {
        console.warn('[Router][nav#' + _navCount + '] ⚠️ Potential freeze culprit detected:', info)
      } else {
        console.log('[Router][nav#' + _navCount + '] ✓ Body clean', {
          nav: _navCount,
          portals: portals.length,
          elAtCenter: info.elAtCenter,
        })
      }
    })
  }
})

router.beforeEach((to, _from, next) => {
  if (to.meta['requiresAuth'] === false) {
    if (oidcService.isLoggedIn() && to.path === '/login') {
      next('/home')
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

export default router
