import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface Tab {
  path: string
  title: string
  /** 首页 tab 不可关闭 */
  closable: boolean
}

const HOME_TAB: Tab = { path: '/home', title: '首页', closable: false }

export const useTabsStore = defineStore('tabs', () => {
  const tabs = ref<Tab[]>([{ ...HOME_TAB }])
  const activeTab = ref<string>(HOME_TAB.path)

  function openTab(path: string, title: string) {
    const exists = tabs.value.find(t => t.path === path)
    if (!exists) {
      tabs.value.push({ path, title, closable: path !== HOME_TAB.path })
    }
    activeTab.value = path
  }

  /** 返回关闭后应跳转的路径，调用方负责 router.push */
  function closeTab(path: string): string {
    const idx = tabs.value.findIndex(t => t.path === path)
    if (idx < 0) return activeTab.value
    tabs.value.splice(idx, 1)
    if (tabs.value.length === 0) tabs.value.push({ ...HOME_TAB })
    if (activeTab.value === path) {
      const next = tabs.value[Math.min(idx, tabs.value.length - 1)]
      activeTab.value = next.path
      return next.path
    }
    return activeTab.value
  }

  function closeOthers(keepPath: string) {
    tabs.value = tabs.value.filter(t => !t.closable || t.path === keepPath)
    activeTab.value = keepPath
  }

  function closeAll(): string {
    tabs.value = [{ ...HOME_TAB }]
    activeTab.value = HOME_TAB.path
    return HOME_TAB.path
  }

  return { tabs, activeTab, openTab, closeTab, closeOthers, closeAll }
})
