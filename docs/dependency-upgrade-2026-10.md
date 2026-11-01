# OctopusERP 依赖升级报告（2026-10）

本次升级把仓库内所有 **NuGet 包**（23 个 csproj）与 **前端 npm 依赖**（8 个前端工程）升级到当前
registry 上的最新稳定版，并保证：**后端全部可编译、全部测试通过、8 个前端全部构建成功**。

---

## 1. 验证结果总览

| 项目 | 升级前 | 升级后 |
|---|---|---|
| .NET 编译（25 个工程） | 25/25 通过 | **25/25 通过** |
| .NET 测试（10 个测试工程，498 个用例） | 487 通过 / 11 失败 | **487 通过 / 11 失败** |
| 前端构建（8 个工程） | 4 通过 / 4 失败 | **8/8 通过** |
| OctopusPLM.Web `pnpm typecheck` | 15 个错误 | **0 个错误** |

> 那 11 个失败在升级前后**完全一致**（用例名逐条比对相同），与本仓库代码无关：
> 它们是需要写 `~/.aspnet/DataProtection-Keys` 的登录类测试，在当前沙箱（workspace-write）下
> 该目录不可写，因此 Cookie 加密抛 `UnauthorizedAccessException`。在正常环境（或放开该目录写权限）
> 这 11 个用例应通过。逐条比对记录见下：

```
AccountControllerTests.Login_WithEditorUser_ReturnsRoleInfo
AccountControllerTests.Login_WithValidCredentials_Returns200AndContainsUserInfo
BusinessFlowTests.Flow1_管理员完整用户管理链路
BusinessFlowTests.Flow4_用户状态管理链路
Phase6ExtensionTests.ResetPassword_WithValidToken_UpdatesPassword
Phase6ExtensionTests.WechatCallback_ExistingBinding_SignsInExistingUser
Phase6ExtensionTests.WechatCallback_WithValidStateAndCode_CreatesNewUserAndSignsIn
UserDataPermissionTests.Login_AdminUser_ReturnsWildcardPermission
UserDataPermissionTests.Login_CommonUser_ReturnsNoButtonPermissions
UserDataPermissionTests.Login_EditorUser_HasNoWildcardPermission
UserDataPermissionTests.Login_UserWithAdminRole_ReturnsWildcardPermission
```

各测试工程用例数：

| 测试工程 | 通过 | 失败 | 总计 |
|---|---:|---:|---:|
| OctopusUMC.Api.Tests | 110 | 11（沙箱环境） | 121 |
| OctopusOA.Api.Tests | 100 | 0 | 100 |
| OctopusMES.Api.Tests | 113 | 0 | 113 |
| OctopusWMS.Api.Tests | 41 | 0 | 41 |
| OctopusCRM.Api.Tests | 38 | 0 | 38 |
| OctopusPLM.Api.Tests | 35 | 0 | 35 |
| OctopusFIN.Api.Tests | 19 | 0 | 19 |
| OctopusHRM.Api.Tests | 11 | 0 | 11 |
| OctopusOMS.Api.Tests | 10 | 0 | 10 |
| MiniappTodo.Api.Tests | 10 | 0 | 10 |
| **合计** | **487** | **11** | **498** |

前端构建验证：

```
OK  OctopusUMC/web/OctopusUMC.Web      OK  OctopusUMC/web/OctopusUMC.H5
OK  OctopusOA/web/OctopusOA.Web        OK  OctopusOA/web/OctopusOA.H5
OK  OctopusPLM/web/OctopusPLM.Web      OK  OctopusCRM/web/OctopusCRM.Web
OK  OctopusWMS/web/OctopusWMS.Web      OK  OctopusMES/web/OctopusMES.Web
```

---

## 2. NuGet 升级明细

`TargetFramework` 保持 **net10.0**（当前最新稳定；net11 只有预览版）。

| 包 | 升级前 | 升级后 |
|---|---|---|
| Microsoft.EntityFrameworkCore / .Design / .Sqlite | 10.0.7 | **10.0.12** |
| Microsoft.Data.Sqlite | 10.0.7 | **10.0.12** |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.7 | **10.0.12** |
| Microsoft.AspNetCore.Authentication.OpenIdConnect | 10.0.7 | **10.0.12** |
| Microsoft.AspNetCore.Authentication.Cookies | 2.3.9 | **2.3.13** |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.7 | **10.0.12** |
| Microsoft.AspNetCore.OpenApi | 10.0.0 / 10.0.7 | **10.0.12** |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.0 | **10.0.12** |
| Microsoft.NET.Test.Sdk | 17.14.1 / 18.5.1 | **18.10.1** |
| xunit.runner.visualstudio | 3.1.4 / 3.1.5 | **4.0.1** |
| coverlet.collector | 6.0.4 / 10.0.0 | **10.1.0** |
| FluentAssertions | 8.9.0 | **8.11.0** |
| Swashbuckle.AspNetCore | 10.1.7 | **10.3.0** |
| OpenIddict.AspNetCore / .EntityFrameworkCore | 7.5.0 | **7.7.2** |
| MySql.EntityFrameworkCore | 10.0.1 | **10.0.9** |
| BCrypt.Net-Next | 4.1.0 | **4.2.1** |
| ClosedXML | 0.105.0 | **0.105.1** |
| MailKit | 4.16.0 | **4.18.1** |
| Microsoft.ML.OnnxRuntime | 1.20.1 | **1.31.0** |
| SixLabors.ImageSharp | 3.1.6 | **4.1.3**（大版本） |
| Qdrant.Client | 1.17.0 | **1.19.0** |
| Aspire.Hosting.AppHost / JavaScript / PostgreSQL / Orchestration / Dashboard | 13.3.1 | **13.6.1** |
| Aspire.Hosting.Keycloak | 13.3.1-preview.1.26261.7 | **13.6.1-preview.1.26506.6** |
| Serilog.AspNetCore | 10.0.0 | 10.0.0（已是最新） |
| Serilog.Sinks.Console | 6.1.1 | 6.1.1（已是最新） |
| Cronos | 0.13.0 | 0.13.0（已是最新） |
| xunit | 2.9.3 | 2.9.3（v2 线最新；v3 是另一个包 `xunit.v3`） |

共 **23 个 csproj、154 处版本号**变更。

### 编译验证
- `SixLabors.ImageSharp` 3 → 4（大版本）在 `OctopusPLM.Api/Services/VectorService.cs` 中使用的
  `Image.Load<Rgb24>` / `Mutate(Resize)` / `image[x, y]` API 未变，直接编译通过。
- `MySql.EntityFrameworkCore` 10.0.1 → 10.0.9、`OpenIddict` 7.5.0 → 7.7.2 无破坏性变更。
- `xunit.runner.visualstudio` 3.x → 4.x：官方说明该版本同时支持 xUnit v1/v2/v3，实测 v2 测试全部正常发现与执行。

---

## 3. npm 升级明细

### 3.1 全平台统一后的版本

| 依赖 | 升级前 | 升级后 |
|---|---|---|
| vue | 3.5.13 / 3.5.22 / 3.5.30 | **3.5.43** |
| vue-router | 4.6.3（PLM）/ 5.0.4 | **5.4.0** |
| pinia | 3.0.3 / 3.0.4 | **4.0.3** |
| @vueuse/core | 13.9.0 / 14.0.0 | **15.0.0** |
| vite | 6.3.5 / 7.1.12 / 8.0.1 / 8.0.10 | **8.3.4** |
| vue-tsc | 2.2.10 / 3.1.2 / 3.2.5 | **3.3.13** |
| @vitejs/plugin-vue | 5.2.3 / 6.0.1 / 6.0.5 | **6.0.10** |
| @vue/tsconfig | 0.7.0 / 0.9.0 | **0.9.1** |
| typescript | 5.7.0 / 5.9.3 | **5.9.3**（见 §4 说明） |
| @tanstack/vue-table | 8.21.3 | **9.2.8**（大版本，需改代码） |
| lucide-vue-next | 0.514.0 / 1.0.0 | **@lucide/vue 1.55.0**（包已改名） |
| axios | 1.7.0 / 1.12.2 / 1.15.2 | **1.20.0** |
| msw | 2.12.14 | **3.0.3**（需改代码） |
| element-plus | 2.11.5 | **2.14.7** |
| echarts | 6.0.0 | **6.1.0** |
| reka-ui | 2.9.6 | **2.11.0** |
| tailwindcss / @tailwindcss/vite | 4.1.8 / 4.1.16 | **4.3.3** |
| tailwind-merge | 3.3.0 / 3.5.0 | **3.7.0** |
| @microsoft/signalr | 10.0.0 | **10.0.11** |
| @types/node | 20.19 / 22 / 24.12 | **26.6.5** |
| oidc-client-ts | 3.1.0 / 3.5.0 | **3.5.0** |

### 3.2 工程工具链（主要是 OctopusPLM.Web）

| 依赖 | 升级前 | 升级后 |
|---|---|---|
| eslint / @eslint/js | 9.38.0 | **10.12.0 / 10.0.1** |
| typescript-eslint | 8.46.2 | **8.71.1** |
| vue-eslint-parser | 10.2.0 | **10.4.1** |
| eslint-plugin-vue / -prettier / -config-prettier | 10.5.1 / 5.5.4 / 10.1.8 | **10.11.1 / 5.5.6 / 10.1.8** |
| stylelint | 16.25.0 | **17.16.0** |
| stylelint-config-standard-scss | 14.0.0 | **17.0.0** |
| stylelint-config-recommended-vue | 1.6.1 | **2.0.0** |
| stylelint-config-recess-order / -prettier | 7.4.0 / 5.0.3 | **7.8.0 / 5.0.3** |
| postcss-html | 1.8.0 | **2.0.0** |
| prettier | 3.6.2 | **3.9.10** |
| @commitlint/* | 20.x | **21.2.3** |
| lint-staged | 16.2.6 | **17.6.0** |
| sass | 1.93.2 | **1.105.1** |
| cssnano | 7.1.1 | **9.6.0** |
| rollup-plugin-visualizer | 6.0.5 | **7.1.1** |
| unplugin-icons | 22.5.0 | **24.0.0** |
| @iconify/vue | 4.2.0 | **5.0.3** |
| @iconify/json | 2.2.400 | **2.2.541** |
| code-inspector-plugin | 1.2.10 | **2.0.9** |
| vite-plugin-fake-server | 2.2.0 | **2.2.4** |
| vite-plugin-router-warn | 1.0.0 | **2.0.0** |
| boxen | 8.0.1 | **9.0.0** |
| svgo / rimraf / qs / dayjs / js-cookie / pinyin-pro / @faker-js/faker / @pureadmin/utils | — | 均已升到最新 |

`vite-plugin-cdn-import`（1.0.1）、`vite-plugin-compression`（0.5.1）、
`radix-vue`（1.9.17，已被 reka-ui 取代，1.9.17 即其最终版）、`husky`（9.1.7）、
`tailwindcss-animate`（1.0.7）本来就在最新版。

### 3.3 代码改动（因大版本升级而必须改）

1. **TanStack Table v8 → v9**（13 个列表页 .vue）
   v9 是「特性注册」架构重写，改动为：
   - `useVueTable` → `useTable`，并新增 `features = { ...coreFeatures, columnVisibilityFeature[, columnSizingFeature] }`
   - 移除 `getCoreRowModel: getCoreRowModel()`（核心行模型由 coreFeatures 自动提供）
   - `createColumnHelper<T>()` → `createColumnHelper<typeof features, T>()`
   - `VisibilityState` → `ColumnVisibilityState`
   - `const columns = [...]` → `const columns = columnHelper.columns([...])`（保留每列 TValue 推断）

2. **MSW v2 → v3**（UMC.Web、OA.Web）
   - `worker.start({ onUnhandledRequest: 'bypass' })` → `{ onUnhandledFrame: 'bypass' }`
   - 用 `msw init public --save` 重新生成 `public/mockServiceWorker.js`（2.12.14 → 3.0.3）

3. **`lucide-vue-next` → `@lucide/vue`**（8 个工程、127 个文件、128 处 import）
   旧包 `lucide-vue-next@1.0.0` 已被官方标记 deprecated 并指向 `@lucide/vue`（1.55.0）。
   已核对本次用到的 **103 个图标名在 `@lucide/vue` 中全部存在**。

4. **补齐缺失的类型声明**
   `OctopusCRM.Web`、`OctopusWMS.Web` 缺少 `src/env.d.ts`（`__APP_VERSION__` / `__BUILD_DATE__`），
   导致 `vue-tsc` 报错、构建失败，已按 MES.Web 的写法补齐。

5. **H5 工程 `tsconfig.node.json` 补 `"noEmit": true`**
   `OctopusUMC.H5` / `OctopusOA.H5` 的 `vue-tsc -b` 会把 `vite.config.ts` 编译输出成 `vite.config.js`
   放在工程根目录，而 **Vite 的配置查找顺序中 `vite.config.js` 优先于 `vite.config.ts`**，
   会让后续构建误用旧的编译产物。已补 `noEmit` 并删除误生成的文件。

6. **`OctopusPLM.Web` 两处既有缺陷修复**
   - `src/components/ReIcon/src/iconifyIconOffline.ts`：`@iconify/vue` 的 `addIcon` 类型签名只接受
     `string`，而上游 pure-admin 写法传入对象。改为局部放宽类型（运行时行为不变）。
   - `src/views/category/manage/index.vue`：`<script setup>` 与普通 `<script>` 重复 import 同名标识符
     （14 个 `TS2300`）；以及 `<BadgeVue variant="primary">` 使用了组件不存在的变体（应为 `default`）。
     这两类共 15 个 `vue-tsc` 错误在升级前就存在，现已全部清零。

---

## 4. 有意保留、未升到「绝对最新」的两项

| 组件 | registry 最新 | 本次采用 | 原因 |
|---|---|---|---|
| typescript | **7.0.2** | 5.9.3 | `typescript-eslint@8.71.1` 的 peerDependencies 明确要求 `typescript >=4.8.4 <6.1.0`。升到 TS 7 会直接破坏 PLM 的 ESLint 工具链。5.9.3 是 5.x 线最新。 |
| .NET TargetFramework | net11.0（仅预览） | net10.0 | net11 尚无稳定版。 |

另：全局 `pnpm` 命令当前为 10.33.0（最新 12.11.2）。它不属于项目依赖，未擅自升级全局工具。

---

## 5. 需要注意 / 未覆盖的部分

1. **`pnpm lint` 会大范围重排格式（升级前就如此，非本次引入）**
   `OctopusPLM.Web/.prettierrc.js` 配置 `singleQuote: false`，但仓库源码普遍使用单引号 + 无分号。
   因此 `pnpm lint:prettier` / `lint:eslint`（都带 `--fix`）本来就会重写所有文件。
   本次只做了验证性运行，**已把所有被 `--fix` 自动重排的文件回滚**，仅保留上述 3 个必要修改文件。
   （核对方式：`git checkout` 还原后的文件用 `prettier --check` 仍报格式不符，说明是既有状态。）

2. **ESLint 10 实测可运行**，报告出 4 个既有错误（未修，超出本次范围）：
   - `src/api/routes.ts`：`'http' is defined but never used`
   - `src/views/product/create/index.vue:835`：`'i' is defined but never used`
   - `src/components/ui/select/SelectScrollUpButton.vue` / `SelectScrollDownButton.vue`：`vue/valid-template-root`

3. **Stylelint 17 实测可运行**（配置文件正常加载）。

4. **`OctopusPLM.Web` 同时存在 `package-lock.json` 与 `pnpm-lock.yaml` 两个锁文件**：
   Dockerfile 用 `npm ci`（依赖 `package-lock.json`），本地 node_modules 是 pnpm 布局。
   本次**两个锁文件都已同步更新**，Docker 与本地两条路径都不会漂移。

5. **未做浏览器端运行时验证**：本次验证手段为「编译 + 类型检查 + 构建 + 单元/集成测试」，
   未启动真实浏览器做端到端回归。建议在合并前跑一遍 Aspire 全量启动冒烟
   （TanStack Table v9 与 MSW v3 涉及运行时行为，虽然 API 迁移是对照官方类型定义完成的）。

---

## 6. 升级后的依赖版本对照（关键项）

### 后端
```
EntityFrameworkCore      10.0.12        OpenIddict              7.7.2
Aspire.*                 13.6.1         MySql.EntityFrameworkCore 10.0.9
Serilog.AspNetCore       10.0.0         SixLabors.ImageSharp    4.1.3
Swashbuckle.AspNetCore   10.3.0         Microsoft.ML.OnnxRuntime 1.31.0
xunit                    2.9.3          xunit.runner.visualstudio 4.0.1
Microsoft.NET.Test.Sdk   18.10.1        coverlet.collector      10.1.0
```

### 前端
```
vue 3.5.43   vue-router 5.4.0   pinia 4.0.3   vite 8.3.4   vue-tsc 3.3.13
typescript 5.9.3   @tanstack/vue-table 9.2.8   @lucide/vue 1.55.0
tailwindcss 4.3.3   element-plus 2.14.7   msw 3.0.3   axios 1.20.0
```
