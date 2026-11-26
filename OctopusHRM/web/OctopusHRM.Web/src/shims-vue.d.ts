/// <reference types="vite/client" />

/**
 * Vue SFC 类型垫片。
 *
 * `vue-tsc -b` 认识 .vue 文件，但 CI 第一步跑的是**裸 tsc**（`npx tsc --noEmit`），
 * 它不认识 .vue，于是 `import App from './App.vue'` 会报
 * TS2307 Cannot find module './App.vue'。这里补上模块声明。
 */
declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<Record<string, unknown>, Record<string, unknown>, unknown>
  export default component
}
