import { ref } from 'vue'

const STORAGE_KEY = 'octopus-theme'
export type Theme = 'light' | 'dark'

function applyTheme(t: Theme) {
  document.documentElement.classList.toggle('dark', t === 'dark')
}

const stored = localStorage.getItem(STORAGE_KEY) as Theme | null
const current = ref<Theme>(stored ?? 'light')
applyTheme(current.value)

// 响应其他标签页（OA）修改主题
window.addEventListener('storage', (e) => {
  if (e.key === STORAGE_KEY && (e.newValue === 'light' || e.newValue === 'dark')) {
    current.value = e.newValue as Theme
    applyTheme(e.newValue as Theme)
  }
})

export function useTheme() {
  function setTheme(t: Theme) {
    current.value = t
    localStorage.setItem(STORAGE_KEY, t)
    applyTheme(t)
  }
  function toggle() {
    setTheme(current.value === 'light' ? 'dark' : 'light')
  }
  return { theme: current, setTheme, toggle }
}
