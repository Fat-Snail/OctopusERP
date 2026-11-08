import type { Component } from 'vue'

// L3 自定义表单组件注册表：业务方把 Vue 组件登记到 id 上，表单 Schema 里用 componentId 引用。
const registry = new Map<string, Component>()

export function registerFormComponent(id: string, component: Component): void {
  registry.set(id, component)
}

export function getFormComponent(id: string): Component | undefined {
  return registry.get(id)
}

export function hasFormComponent(id: string): boolean {
  return registry.has(id)
}
