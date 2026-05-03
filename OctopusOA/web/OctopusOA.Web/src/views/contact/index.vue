<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Search, RefreshCw } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { getDeptTree, getUsers, getUser } from '@/api/contact'
import type { ContactDeptNode, ContactUser } from '@/api/contact/types'
import UserCard from './components/UserCard.vue'
import UserDetail from './components/UserDetail.vue'

const deptTree = ref<ContactDeptNode[]>([])
const users = ref<ContactUser[]>([])
const loading = ref(false)
const keyword = ref('')
const selectedDeptId = ref<number | undefined>(undefined)
const selectedDeptName = ref('')
const detailVisible = ref(false)
const selectedUser = ref<ContactUser | null>(null)
const expandedDepts = ref<Set<number>>(new Set())

async function loadTree() {
  deptTree.value = await getDeptTree()
  // expand all by default
  const expand = (nodes: ContactDeptNode[]) => {
    nodes.forEach(n => { expandedDepts.value.add(n.deptId); if (n.children) expand(n.children) })
  }
  expand(deptTree.value)
}

async function loadUsers() {
  loading.value = true
  try {
    const data = await getUsers({ deptId: selectedDeptId.value, keyword: keyword.value || undefined })
    users.value = data.rows
  } finally { loading.value = false }
}

function handleDeptClick(node: ContactDeptNode) {
  selectedDeptId.value = node.deptId
  selectedDeptName.value = node.deptName
  loadUsers()
}

function toggleDept(deptId: number) {
  if (expandedDepts.value.has(deptId)) expandedDepts.value.delete(deptId)
  else expandedDepts.value.add(deptId)
}

function resetFilter() {
  keyword.value = ''
  selectedDeptId.value = undefined
  selectedDeptName.value = ''
  loadUsers()
}

async function openDetail(user: ContactUser) {
  selectedUser.value = await getUser(user.umcUserId)
  detailVisible.value = true
}

onMounted(async () => {
  await loadTree()
  await loadUsers()
})
</script>

<template>
  <div>
    <PageHeader title="通讯录" />
    <div class="p-5 flex gap-4 h-[calc(100vh-var(--topbar-h,48px)-56px)] overflow-hidden">
      <!-- Left dept tree -->
      <div class="w-[240px] shrink-0 bg-card border border-border rounded-[var(--radius-lg)] overflow-y-auto flex flex-col">
        <div class="flex items-center justify-between px-3 py-2 border-b border-border">
          <span class="text-[12px] font-semibold text-foreground">组织架构</span>
          <button class="p-1 text-muted-foreground hover:text-foreground cursor-pointer border-0 bg-transparent" @click="loadTree">
            <RefreshCw :size="12" />
          </button>
        </div>
        <div class="flex-1 overflow-y-auto p-2">
          <DeptNode
            v-for="node in deptTree"
            :key="node.deptId"
            :node="node"
            :selected-id="selectedDeptId"
            :expanded="expandedDepts"
            @select="handleDeptClick"
            @toggle="toggleDept"
          />
        </div>
      </div>

      <!-- Right user area -->
      <div class="flex-1 min-w-0 flex flex-col bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <div class="flex items-center gap-2 px-3 py-2 border-b border-border">
          <div class="relative flex-1 max-w-[280px]">
            <Search :size="12" class="absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              v-model="keyword"
              type="text"
              placeholder="搜索姓名/手机号/邮箱"
              class="w-full h-[var(--row-h)] pl-8 pr-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring"
              @keyup.enter="loadUsers"
            />
          </div>
          <button
            class="h-[var(--row-h)] px-3 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0"
            @click="loadUsers"
          >搜索</button>
          <button
            class="h-[var(--row-h)] px-3 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card"
            @click="resetFilter"
          >重置</button>
          <span v-if="selectedDeptName" class="text-[12px] text-muted-foreground ml-2">
            部门：<strong>{{ selectedDeptName }}</strong>
          </span>
          <span class="ml-auto text-[11px] text-muted-foreground">共 {{ users.length }} 人</span>
        </div>

        <div class="flex-1 overflow-y-auto p-3">
          <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
          <div v-else-if="!users.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无员工</div>
          <div v-else class="grid gap-2" style="grid-template-columns: repeat(auto-fill, minmax(280px, 1fr))">
            <UserCard v-for="u in users" :key="u.umcUserId" :user="u" @click="openDetail(u)" />
          </div>
        </div>
      </div>
    </div>

    <Sheet v-model:open="detailVisible">
      <SheetContent side="right" class="w-[460px]">
        <SheetHeader><SheetTitle>员工详情</SheetTitle></SheetHeader>
        <UserDetail :user="selectedUser" />
      </SheetContent>
    </Sheet>
  </div>
</template>

<script lang="ts">
// Recursive dept node component
import { defineComponent, h, type PropType, type Component } from 'vue'
import { ChevronRight } from 'lucide-vue-next'

const DeptNode: Component = defineComponent({
  name: 'DeptNode',
  props: {
    node: { type: Object as PropType<ContactDeptNode>, required: true },
    selectedId: { type: Number as PropType<number | undefined>, default: undefined },
    expanded: { type: Object as PropType<Set<number>>, required: true },
  },
  emits: ['select', 'toggle'],
  setup(props, { emit }) {
    return () => {
      const isExpanded = props.expanded.has(props.node.deptId)
      const isSelected = props.selectedId === props.node.deptId
      const hasChildren = props.node.children && props.node.children.length > 0

      return h('div', null, [
        h('button', {
          class: [
            'w-full text-left flex items-center gap-1 px-2 py-1.5 rounded-[var(--radius)] text-[12px] cursor-pointer border-0 transition-colors',
            isSelected ? 'bg-primary/10 text-primary font-medium' : 'text-foreground hover:bg-muted',
          ].join(' '),
          onClick: () => emit('select', props.node),
        }, [
          hasChildren
            ? h('span', {
                class: ['transition-transform shrink-0', isExpanded ? 'rotate-90' : ''].join(' '),
                onClick: (e: Event) => { e.stopPropagation(); emit('toggle', props.node.deptId) },
              }, h(ChevronRight, { size: 12 }))
            : h('span', { class: 'w-3 shrink-0' }),
          h('span', { class: 'flex-1 truncate' }, props.node.deptName),
          h('span', { class: 'text-[10px] text-muted-foreground shrink-0' }, props.node.userCount),
        ]),
        isExpanded && hasChildren
          ? h('div', { class: 'ml-3' },
              props.node.children!.map(child =>
                h(DeptNode, {
                  key: child.deptId,
                  node: child,
                  selectedId: props.selectedId,
                  expanded: props.expanded,
                  onSelect: (n: ContactDeptNode) => emit('select', n),
                  onToggle: (id: number) => emit('toggle', id),
                })
              )
            )
          : null,
      ])
    }
  },
})
export { DeptNode }
</script>
