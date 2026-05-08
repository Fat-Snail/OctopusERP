import type { DefineComponent } from 'vue'

declare module 'vue' {
  export interface GlobalComponents {
    // App shell
    AppShell: DefineComponent<Record<string, unknown>>
    ModuleRail: DefineComponent<{ activeModule: string }>
    SubMenu: DefineComponent<Record<string, unknown>>
    TopBar: DefineComponent<Record<string, unknown>>
    TabBar: DefineComponent<Record<string, unknown>>
    PageHeader: DefineComponent<{ title?: string; sub?: string; noBorder?: boolean }>

    // UI primitives
    AvatarVue: DefineComponent<{ fallback?: string; size?: 'sm' | 'lg' }>
    BadgeVue: DefineComponent<{ variant?: string }>
    ButtonVue: DefineComponent<{ variant?: string; size?: string; disabled?: boolean }>
    CardVue: DefineComponent<Record<string, unknown>>
    CardHeaderVue: DefineComponent<Record<string, unknown>>
    CardContentVue: DefineComponent<Record<string, unknown>>
    DrawerVue: DefineComponent<{ open?: boolean; side?: string }>
    InputVue: DefineComponent<{ modelValue?: string; placeholder?: string }>
    SeparatorVue: DefineComponent<Record<string, unknown>>

    // Router
    RouterLink: (typeof import('vue-router'))['RouterLink']
    RouterView: (typeof import('vue-router'))['RouterView']
  }
}

export {}
