# Design Tokens — OctopusERP H5

## 颜色

### 品牌主色

| Token | 值 | 用途 |
|---|---|---|
| `--primary` | `#2563eb`（蓝色） | 主按钮、激活状态、链接 |
| `--primary-soft` | `oklch(0.96 0.03 250)`（浅蓝底） | 轻量 Badge 背景、激活菜单底色 |
| `--primary-soft-fg` | `oklch(0.45 0.18 250)` | primary-soft 上的文字 |

### 语义色

| Token | 值 | 用途 |
|---|---|---|
| `--success` | `oklch(0.62 0.14 155)`（绿） | 正常/通过/完成 |
| `--warning` | `oklch(0.72 0.16 75)`（橙） | 待处理/迟到/警告 |
| `--danger` | `oklch(0.6 0.2 25)`（红） | 驳回/异常/危险 |
| `--info` | `oklch(0.62 0.14 220)`（蓝灰） | 信息提示/进行中 |

### 中性色

| Token | 值 | 用途 |
|---|---|---|
| `--background` | `#ffffff` | 页面背景 |
| `--foreground` | `#0a0a0a` | 主文字 |
| `--muted` | `#f1f5f9` | 次级背景、分割区 |
| `--muted-foreground` | `#64748b` | 辅助文字、占位符 |
| `--border` | `#e2e8f0` | 分割线、边框 |
| `--subtle` | `#fafafa` | 列表行 hover、卡片背景 |

### Badge 颜色规范（必须用 color-mix，禁止写死）

```css
/* 成功/通过/正常 */
bg: color-mix(in oklch, var(--success) 14%, transparent)
fg: var(--success)

/* 警告/待审批/迟到 */
bg: color-mix(in oklch, var(--warning) 18%, transparent)
fg: var(--warning)

/* 危险/驳回/异常 */
bg: color-mix(in oklch, var(--danger) 14%, transparent)
fg: var(--danger)

/* 信息/进行中 */
bg: color-mix(in oklch, var(--info) 14%, transparent)
fg: var(--info)

/* 中性/未打卡/草稿 */
bg: var(--muted)
fg: var(--muted-foreground)
```

---

## 字体

| 场景 | 字号 | 字重 |
|---|---|---|
| 页面大标题 | 18px | 600 |
| 卡片标题 / 列表主文字 | 15px | 500 |
| 正文 / 表单标签 | 14px | 400 |
| 辅助文字 / 时间戳 | 12px | 400 |
| 极小标注 | 11px | 400 |

字体栈：`"PingFang SC", "Source Han Sans CN", "Microsoft YaHei", system-ui, sans-serif`

数字/编码：`"JetBrains Mono", "SF Mono", ui-monospace, monospace`（tabular-nums）

---

## 间距

| 名称 | 值 | 场景 |
|---|---|---|
| 页面横向内边距 | 16px | 页面左右 padding |
| 卡片内边距 | 16px | 卡片内容区 |
| 列表行高 | 52px min | 点击区域 ≥ 44px（iOS 规范） |
| 元素间距 | 8 / 12 / 16px | 标准间距梯度 |
| 底部安全区 | `env(safe-area-inset-bottom)` | 底部导航 + CTA 按钮 |

---

## 圆角

| 场景 | 值 |
|---|---|
| 卡片 | 12px |
| 按钮 | 8px |
| Badge / 芯片 | 999px（胶囊） |
| 输入框 | 8px |
| 底部弹层（Sheet） | 16px 顶部两角 |
| 模态框 | 16px |

---

## 组件规范

### 顶部导航栏（NavBar）

```
高度：56px（含状态栏 20px）
背景：white，底部 1px border
左：返回箭头（ChevronLeft, 24px）/ Logo
中：页面标题（15px, 600）
右：操作按钮（文字或图标，最多1-2个）
```

### 底部导航（TabBar）

```
高度：50px + safe-area-inset-bottom
图标：22px，激活色 var(--primary)
标签：10px，激活色 var(--primary)
背景：white，顶部 1px border
```

### 列表项（ListItem）

```
高度：min 52px，padding 0 16px
主文字：14px foreground
副文字：12px muted-foreground
右侧：ChevronRight 或 Badge
分割线：1px border，左缩进 16px
```

### 底部弹层（ActionSheet / Sheet）

```
从底部滑入，顶部圆角 16px
顶部拖拽条：32×4px，圆角 2px，颜色 border
蒙层：black/40%
```

### 主按钮（Primary Button）

```
高度：48px
宽度：撑满容器（全宽场景）
背景：var(--primary)
文字：白色，15px，500
圆角：8px
底部留出 safe-area-inset-bottom
```
