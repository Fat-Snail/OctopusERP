/// <reference types="vite/client" />

interface ImportMetaEnv {
  /**
   * Keycloak realm 的基础地址，形如 http://localhost:8088/realms/octopus
   * 未配置时回退到本地默认值（见 services/oidcService.ts）
   */
  readonly VITE_KEYCLOAK_AUTHORITY?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
