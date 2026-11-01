# OctopusERP — UI 规范（shadcn-vue 迁移）

## 一、概述与迁移决策

### 现状 → 目标

| 项目 | 现状 | 目标 |
|---|---|---|
| 组件库 | Element Plus（pure-admin-thin） | **shadcn-vue** |
| CSS 方案 | UnoCSS（atomic） | **Tailwind CSS v4** |
| 图标 | Element Plus Icons | **lucide-vue-next** |
| 表格 | el-table | **@tanstack/vue-table** |
| 布局 | pure-admin 内置壳 | **自定义三列壳（Module Rail + SubMenu + Content）** |

### 方案：各 App 独立脚手架

UMC / OA / PLM 三个前端**各自重建**，不共享 npm package（避免跨仓库依赖复杂度）。
设计 Token 和 Shell 组件通过**复制粘贴 + 统一规范**保持一致。

后端（.NET 8 + EF Core + SQLite）**不动**，API 契约不变，仅换前端。

---

## 二、技术栈

```
Vue 3.5+            Composition API，<script setup lang="ts">
TypeScript 5        strict: true，noImplicitAny
Vite 6              构建，本地代理 /api → 后端端口
Tailwind CSS v4     @import "tailwindcss"（CSS-first，无 tailwind.config）
shadcn-vue          headless 组件，已包含 Radix Vue 依赖
lucide-vue-next     图标（与设计稿 Lucide 风格一致）
@tanstack/vue-table 高密度可排序/筛选表格
Pinia               状态管理（AppShell UI 状态 + 用户会话）
vue-router 4        路由（与现有后端 OIDC 回调路径保持兼容）
oidc-client-ts      OA / PLM SSO（已有）
```

### 包安装命令（新建 App 时使用）

```bash
npm create vite@latest my-app -- --template vue-ts
cd my-app
npx shadcn-vue@latest init          # 交互式配置 → 选 Zinc 基础色，Neutral gray，CSS 变量
npm install -D tailwindcss@next @tailwindcss/vite
npm install lucide-vue-next @tanstack/vue-table pinia vue-router oidc-client-ts
```

---

## 三、Design Tokens（globals.css）

每个 App 的 `src/styles/globals.css`：

```css
@import "tailwindcss";

/* ── shadcn-vue 默认 tokens（不覆盖的保持原值） ── */
@layer base {
  :root {
    /* shadcn-vue 要求的 CSS 变量（供 Tailwind utilities 引用） */
    --background: 0 0% 100%;
    --foreground: 0 0% 3.9%;
    --card: 0 0% 100%;
    --card-foreground: 0 0% 3.9%;
    --popover: 0 0% 100%;
    --popover-foreground: 0 0% 3.9%;

    /* ── 品牌色（ERP 设计稿蓝）─ shadcn HSL 格式 ── */
    --primary: 221 83% 53%;        /* oklch(0.55 0.18 250) ≈ #2563eb */
    --primary-foreground: 0 0% 100%;

    --secondary: 210 40% 96.1%;
    --secondary-foreground: 222.2 47.4% 11.2%;
    --muted: 210 40% 96.1%;
    --muted-foreground: 215.4 16.3% 46.9%;
    --accent: 210 40% 96.1%;
    --accent-foreground: 222.2 47.4% 11.2%;
    --destructive: 0 84% 60%;
    --destructive-foreground: 0 0% 98%;
    --border: 214.3 31.8% 91.4%;
    --input: 214.3 31.8% 91.4%;
    --ring: 221 83% 53%;

    --radius: 0.375rem;            /* 6px — 设计稿 --radius */

    /* ── ERP 扩展色（设计稿 semantic colors）── */
    --success: oklch(0.62 0.14 155);
    --warning: oklch(0.72 0.16 75);
    --danger:  oklch(0.6 0.2 25);
    --info:    oklch(0.62 0.14 220);
    --primary-soft: oklch(0.96 0.03 250);
    --primary-soft-fg: oklch(0.45 0.18 250);
    --subtle: oklch(0.99 0 0);
    --border-strong: oklch(0.88 0 0);

    /* ── 字体 ── */
    --font-sans: "Inter", "PingFang SC", "Source Han Sans CN", "Microsoft YaHei", system-ui, sans-serif;
    --font-mono: "JetBrains Mono", "SF Mono", ui-monospace, Menlo, monospace;

    /* ── 密度（compact=0.85 / standard=1 / comfy=1.15）── */
    --density: 1;
    --row-h:  calc(28px * var(--density));
    --pad-x:  calc(12px * var(--density));
    --pad-y:  calc(8px * var(--density));
    --gap:    calc(8px * var(--density));
  }

  .dark {
    --background: 0 0% 3.9%;
    --foreground: 0 0% 98%;
    --card: 0 0% 6.7%;
    --card-foreground: 0 0% 98%;
    --popover: 0 0% 6.7%;
    --popover-foreground: 0 0% 98%;
    --primary: 221 83% 67%;
    --primary-foreground: 0 0% 3.9%;
    --muted: 0 0% 10.5%;
    --muted-foreground: 0 0% 54%;
    --border: 0 0% 13.7%;
    --input: 0 0% 13.7%;
    --primary-soft: oklch(0.28 0.08 250);
    --primary-soft-fg: oklch(0.85 0.1 250);
    --subtle: oklch(0.09 0 0);
    --success: oklch(0.7 0.14 155);
    --warning: oklch(0.78 0.15 75);
    --danger:  oklch(0.68 0.2 25);
    --info:    oklch(0.7 0.14 220);
  }
}

/* ── 全局 reset ── */
@layer base {
  * { @apply border-border; }
  body {
    @apply bg-background text-foreground;
    font-family: var(--font-sans);
    font-size: 13px;
    line-height: 1.5;
    font-feature-settings: "cv11", "ss01";
    -webkit-font-smoothing: antialiased;
  }
}

/* ── ERP 工具类（非 Tailwind utility，设计稿直接对应） ── */
@layer utilities {
  .font-mono-tnum {
    font-family: var(--font-mono);
    font-feature-settings: "tnum";
  }
  /* 密度变体（data-density="compact/comfy" 挂在 <html> 或 <body>） */
  [data-density="compact"] { --density: 0.85; }
  [data-density="comfy"]   { --density: 1.15; }
}
```

---

## 四、应用壳（App Shell）

### 布局结构

```
┌────────┬──────────────┬─────────────────────────────────────────┐
│ Module │  Sub Menu    │  TopBar (h-12)                          │
│  Rail  │  (w-[200px]) ├─────────────────────────────────────────┤
│(w-14)  │              │  PageHeader                             │
│        │              │  title / sub / actions / tabs           │
│        │              ├─────────────────────────────────────────┤
│        │              │  <slot /> — 页面内容（overflow-y-auto）  │
└────────┴──────────────┴─────────────────────────────────────────┘
```

### Module Rail（56px）

- Logo 区域：32×32 圆角色块
- 模块列表：40×40 图标按钮，激活态 `bg-[var(--primary-soft)] text-[var(--primary-soft-fg)]`
- 底部：Settings 图标
- 模块定义（全局共用，未实装的显示为 disabled）：

```
workbench  工作台
user       用户中心     → UMC 管辖
oa         OA 协同     → OA 管辖
plm        PLM         → PLM 管辖
wms        WMS         → 占位（未来）
mes        MES         → 占位（未来）
crm        CRM         → 占位（未来）
```

### Sub Menu（200px）

- 模块标题区：模块中文名 + 英文 MONO
- 二级菜单分组：`text-xs uppercase tracking-wide` 的 group 标签 + nav items
- 激活项：`erp-nav-item active-primary`（primary-soft 背景）
- 徽标：未读数 badge（primary 变体）

### TopBar（48px）

- 左：面包屑（12px / muted-foreground，末项 foreground 加粗）
- 中：弹性占位
- 中右：全局搜索（⌘K，280px，muted 背景）
- 右：消息铃（带红点 badge）+ 用户头像（首字母 Avatar）

### PageHeader

- 标题（18px / 600）+ 副标题（12px / muted）
- 右侧 actions slot
- 可选 Tabs（border-bottom 2px primary 激活线）

### 文件位置

```
src/components/app/
├── AppShell.vue      ← Shell 总包装
├── ModuleRail.vue    ← 56px 竖列
├── SubMenu.vue       ← 200px 侧边
├── TopBar.vue        ← 48px 顶栏
└── PageHeader.vue    ← 标题区
```

---

## 五、shadcn-vue 组件清单（需 `npx shadcn-vue@latest add` 安装）

| 设计稿 | shadcn-vue 组件 | 备注 |
|---|---|---|
| `.erp-btn` | `Button` variant=outline/default/ghost | sm size=28px（--row-h） |
| `.erp-input` | `Input` | h-[var(--row-h)] |
| `.erp-card / -hd / -body` | `Card CardHeader CardContent` | border-radius 6px |
| `.erp-badge` | `Badge` + 自定义 success/warning/danger/info variant | 见下方 |
| `.erp-tabs / -tab` | `Tabs TabsList TabsTrigger` | underline 变体 |
| `.erp-checkbox` | `Checkbox` | 14px，自动 |
| `.erp-avatar` | `Avatar AvatarFallback` | 24px sm / 40px lg |
| `.erp-progress` | `Progress` | h-1.5 |
| `.erp-table` | `@tanstack/vue-table` + `Table` headless | 见表格规范 |
| `.erp-nav-item` | 自定义 NavItem（基于 `Button` ghost） | active-primary 变体 |
| `cmdk` 搜索 | `CommandDialog` | ⌘K 全局搜索 |
| `.erp-kbd` | 自定义 `<Kbd>` 组件 | 内联 kbd 样式 |
| Dialog / AlertDialog | **自定义实现**（非 shadcn-vue）| `provide/inject` + `<Teleport to="body">` + `v-if`；零 reka-ui 依赖 |
| Sheet（抽屉） | **自定义实现**（非 shadcn-vue）| 同上；side 由内部 record 映射到 Tailwind 类 |
| Select | **自定义实现**（非 shadcn-vue）| `provide/inject` + `position: absolute` dropdown；无 Teleport portal |

> **说明**：Dialog / Sheet / Select 因 reka-ui 模块级单例状态在 SPA 导航中不能可靠清理（约 15 次导航后 UI 冻结），已全部替换为零 reka-ui 依赖的自定义组件。详见 `DECISIONS.md`「OA.Web：Dialog / Sheet / Select 组件替换」。外观和 props 接口与原 shadcn-vue 版本保持兼容，消费代码无需修改。

### Badge 扩展 variant（在 `src/components/ui/badge/index.ts` 中扩展）

```typescript
variants: {
  variant: {
    default: "...",
    secondary: "...",
    destructive: "...",
    outline: "...",
    // ERP 扩展
    success: "bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)] border-transparent",
    warning: "bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)] border-transparent",
    danger:  "bg-[color-mix(in_oklch,var(--danger)_14%,transparent)] text-[var(--danger)] border-transparent",
    info:    "bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)] border-transparent",
    primary: "bg-[var(--primary-soft)] text-[var(--primary-soft-fg)] border-transparent",
  }
}
```

---

## 六、表格规范（@tanstack/vue-table）

所有列表页使用 TanStack Table，不用 Element Plus el-table：

```vue
<!-- 必须遵守的模式 -->
<script setup lang="ts">
import { useVueTable, getCoreRowModel, createColumnHelper } from '@tanstack/vue-table'
const columnHelper = createColumnHelper<RowType>()
const columns = [
  columnHelper.accessor('code', {
    header: '编码',
    cell: info => h('span', { class: 'font-mono-tnum text-[11px]' }, info.getValue()),
  }),
  // ...
]
const table = useVueTable({ data, columns, getCoreRowModel: getCoreRowModel() })
</script>
<template>
  <Table>
    <TableHeader>
      <TableRow v-for="hg in table.getHeaderGroups()" :key="hg.id">
        <TableHead v-for="h in hg.headers" :key="h.id"
          class="text-[11px] uppercase tracking-wide bg-[var(--subtle)]">
          {{ h.column.columnDef.header }}
        </TableHead>
      </TableRow>
    </TableHeader>
    <TableBody>
      <TableRow v-for="row in table.getRowModel().rows" :key="row.id"
        class="hover:bg-[var(--subtle)]">
        <TableCell v-for="cell in row.getVisibleCells()" :key="cell.id"
          class="py-[7px] px-3 text-[12px]">
          <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
        </TableCell>
      </TableRow>
    </TableBody>
  </Table>
</template>
```

**规则**：
- 数字 / ID / 金额 / 编码列：`class="font-mono-tnum text-right"`
- 表头：`text-[11px] uppercase tracking-[0.04em]`
- 行高：`h-[var(--row-h)]`（约 28px），通过 CSS 变量响应密度开关

---

## 七、排版规则

| 用途 | 类名 | 大小 |
|---|---|---|
| 卡片标题 | `text-[14px] font-semibold` | 14px |
| 正文 | `text-[13px]` | 13px（body 默认） |
| 表格内容 | `text-[12px]` | 12px |
| Caption / 辅助文字 | `text-[11px] text-muted-foreground` | 11px |
| 菜单分组标签 | `text-[10px] uppercase tracking-[0.06em]` | 10px |
| 数字（编码/金额） | `font-mono-tnum text-[12px]` | 12px mono |

---

## 八、图标规范

全用 `lucide-vue-next`，不混用 Element Plus Icons。

```vue
<script setup lang="ts">
import { Plus, Search, ChevronDown, MoreHorizontal, Check, X } from 'lucide-vue-next'
</script>
<template>
  <Plus :size="13" class="shrink-0" />
</template>
```

**大小规则**：
- 按钮内图标：`:size="13"`
- 导航图标：`:size="18"`
- 操作图标（More / Edit / Eye）：`:size="14"`
- TopBar 图标：`:size="16"`

---

## 九、各 App 页面范围

### UMC.Web（用户中心）

| 页面 | 路由 | 对应设计稿 |
|---|---|---|
| 工作台 | `/workbench` | `workbench.jsx` |
| 组织架构 | `/user/org` | `pages/user.jsx → OrgPage` |
| 权限矩阵 | `/user/permissions` | `pages/user.jsx → PermissionMatrixPage` |
| 登录 | `/login` | — |
| 回调 | — | 无（UMC 自己是 IdP，无 OIDC 回调） |

### OA.Web（OA 协同）

| 页面 | 路由 | 对应设计稿 |
|---|---|---|
| 工作台待办 | `/oa` | `workbench.jsx` + `oa.jsx` 待办部分 |
| 审批流详情 | `/oa/approval/:id` | `pages/oa.jsx → OAApprovalFlowPage` |
| 移动端待办 | `/m/oa/todo` | `pages/oa.jsx → OAMobileTodo` |
| 其余审批列表、考勤、会议、通讯录 | 现有路由 | 按旧功能套新壳 |
| 登录 / 回调 | `/login` `/callback` | — |

### PLM.Web（商品管理）

| 页面 | 路由 | 对应设计稿 |
|---|---|---|
| 商品列表（物料主数据） | `/plm/product` | `pages/plm.jsx → MaterialMasterPage` |
| BOM 管理（未来） | `/plm/bom` | `pages/plm.jsx → BOMPage` |
| 商品新增 / 编辑 | `/plm/product/create` | — |
| 登录 / 回调 | `/login` `/callback` | — |

---

## 十、目录结构模板（每个 App 统一）

```
src/
├── components/
│   ├── ui/               # shadcn-vue 自动生成（npx shadcn-vue add ...）
│   │                     # 例外：dialog/ sheet/ select/ 为自定义实现（零 reka-ui）
│   └── app/              # AppShell / ModuleRail / SubMenu / TopBar / PageHeader
├── layouts/
│   └── default.vue       # <AppShell> 包裹 <router-view>
├── views/                # 页面级组件（对应路由）
├── api/                  # 请求函数 + types.ts（不变）
├── store/
│   ├── shell.ts          # 侧边栏状态（当前模块 / 密度 / 主题）
│   └── user.ts           # OIDC 用户信息
├── router/
│   └── index.ts          # 简洁手写路由（参考 OA 现有写法）
├── services/
│   └── oidcService.ts    # oidc-client-ts（OA / PLM 有，UMC 无）
└── styles/
    └── globals.css       # 上方 Token 全文
```

---

## 十一、实施顺序

```
Step 1  全局壳（AppShell / ModuleRail / SubMenu / TopBar / PageHeader）
        → 一次实现，UMC/OA/PLM 复用同一组文件

Step 2  UMC.Web（用户中心）
        → 2a 登录页
        → 2b 组织架构
        → 2c 权限矩阵
        → 2d 工作台首页

Step 3  OA.Web
        → 3a 审批流详情（最复杂，先做）
        → 3b 工作台待办
        → 3c 套壳：审批列表 / 考勤 / 会议室 / 通讯录（旧功能，换 UI）

Step 4  PLM.Web
        → 4a 商品列表（物料主数据风格）
        → 4b 商品新增 / 编辑表单

Step 5  主题 + 密度开关
        → AppShell 顶部 Settings 入口
        → localStorage 持久化 theme / density
```

每完成一步，跑 `npx tsc --noEmit && npm run build` 验证零报错后进入下一步。

---

## 十二、禁止事项

- **禁止** 混用 Element Plus 组件（包括 `el-table`, `el-button`, `el-message`）
- **禁止** 使用 UnoCSS / `un:` 前缀（已移除）
- **禁止** 在 `.vue` 文件中写内联 `style="..."` （使用 Tailwind utility classes）；仅 CSS 变量赋值例外
- **禁止** `any`，用 `unknown` + 类型收窄代替
- **禁止** 在组件内内联 interface（统一放 `src/api/*/types.ts`）
- **Table 必须**用 `@tanstack/vue-table`，不许直接写 `<tr><td>`（除非是静态展示单元）
- **数字 / ID / 金额 / 编码**必须使用 `font-mono-tnum` class
- **禁止**在 OA.Web（或任何需要 SPA 多路由切换的 App）中直接使用 reka-ui 的 `DismissableLayer`、`FocusScope`、`Presence`、`useBodyScrollLock` 原语，这些组件持有模块级单例状态，会在 SPA 导航后累积导致 UI 冻结。需要 Dialog / Sheet / Select 等弹出层时，使用 `src/components/ui/` 下已有的自定义实现。
