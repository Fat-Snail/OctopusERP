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
  // ── H5 mobile routes ──
  {
    path: '/h5',
    component: () => import('@/layouts/H5Layout.vue'),
    meta: { requiresAuth: true },
    redirect: '/h5/home',
    children: [
      {
        path: 'home',
        name: 'H5Home',
        component: () => import('@/views/h5/home/index.vue'),
        meta: { title: '首页' }
      },
      {
        path: 'approval',
        name: 'H5Approval',
        component: () => import('@/views/h5/approval/index.vue'),
        meta: { title: '审批' }
      },
      {
        path: 'attendance',
        name: 'H5Attendance',
        component: () => import('@/views/h5/attendance/index.vue'),
        meta: { title: '考勤' }
      },
      {
        path: 'profile',
        name: 'H5Profile',
        component: () => import('@/views/h5/profile/index.vue'),
        meta: { title: '我的' }
      },
    ]
  },
  // H5 pages outside the TabBar layout (full screen with back button)
  {
    path: '/h5/approval/detail/:id',
    name: 'H5ApprovalDetail',
    component: () => import('@/views/h5/approval/detail/index.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/h5/notice',
    name: 'H5Notice',
    component: () => import('@/views/h5/notice/index.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/h5/notice/:id',
    name: 'H5NoticeDetail',
    component: () => import('@/views/h5/notice/detail/index.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/h5/contact',
    name: 'H5Contact',
    component: () => import('@/views/h5/contact/index.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/h5/meeting',
    name: 'H5Meeting',
    component: () => import('@/views/h5/meeting/index.vue'),
    meta: { requiresAuth: true }
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

const isMobile = () => window.matchMedia('(max-width: 767px)').matches

// Desktop path → H5 equivalent (best-effort, falls back to /h5/home)
const H5_MAP: Record<string, string> = {
  '/home': '/h5/home',
  '/approval/apply': '/h5/approval',
  '/approval/mine': '/h5/approval',
  '/approval/pending': '/h5/approval',
  '/attendance/mine': '/h5/attendance',
  '/notice/list': '/h5/notice',
  '/contact': '/h5/contact',
  '/meeting/mine': '/h5/meeting',
  '/profile': '/h5/profile',
}

router.beforeEach((to, _from, next) => {
  const mobile = isMobile()
  const isH5Path = to.path.startsWith('/h5/')
  const isPublicPath = to.meta['requiresAuth'] === false

  // Mobile visiting desktop route → redirect to H5
  if (mobile && !isH5Path && !isPublicPath) {
    const h5 = H5_MAP[to.path] ?? '/h5/home'
    next(h5)
    return
  }

  // Desktop visiting H5 route → redirect to desktop home
  if (!mobile && isH5Path) {
    next('/home')
    return
  }

  if (isPublicPath) {
    if (oidcService.isLoggedIn() && to.path === '/login') {
      const saved = sessionStorage.getItem('oa_redirect')
      sessionStorage.removeItem('oa_redirect')
      next(saved || (mobile ? '/h5/home' : '/home'))
    } else {
      next()
    }
  } else {
    if (!oidcService.isLoggedIn()) {
      if (to.path !== '/callback') {
        sessionStorage.setItem('oa_redirect', to.fullPath)
      }
      next('/login')
    } else {
      next()
    }
  }
})

export default router
