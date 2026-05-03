/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** 是否启用 MSW Mock（'true' 开启，'false' 关闭） */
  readonly VITE_ENABLE_MOCK: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

declare const __APP_VERSION__: string
declare const __BUILD_DATE__: string
