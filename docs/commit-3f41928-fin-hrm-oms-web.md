# 提交说明：`3f41928` — 补齐 FIN / HRM / OMS 三个管理前端

> 对应需求：`FEATURE_GAPS.md` 的 **FR-01 / FR-02 / FR-03**
> 提交时间：2026-11-27 07:13 +0800 ｜ 提交者：Fat Snail
> 提交标题：`feat(web): add FIN/HRM/OMS management frontends`

---

## 1. 背景与目标

代码核查发现：9 个后端子系统里原本只有 6 个有 Web UI（UMC / OA / PLM / CRM / WMS / MES），
财务（FIN）、人力（HRM）、订单中台（OMS）三个系统只能靠 Swagger / curl 操作；刚完成的淘宝
adapter 也没有界面，触发拉单、查看适配器状态都要手敲命令。

本次提交补齐这三个缺失的管理前端，统一采用 **Vite + Vue 3 + TypeScript**，接入统一 OIDC 登录，
并纳入 Aspire 编排与 CI 构建矩阵。

---

## 2. 提交内容

### 2.1 三个新增前端

| 系统 | 目录 | 端口 | 页面 | 文件数 / 行数 |
|---|---|---:|---|---:|
| OMS 渠道管理 | `OctopusOMS/web/OctopusOMS.Web` | 5183 | 渠道店铺、手动拉单、适配器诊断、订单、SKU 映射、售后 | 36 / +4291 |
| FIN 财务管理 | `OctopusFIN/web/OctopusFIN.Web` | 5181 | 凭证（多分录）、科目、应收应付、期间月结、报表、概览 | 37 / +4423 |
| HRM 人力管理 | `OctopusHRM/web/OctopusHRM.Web` | 5182 | 员工档案、薪资核算、请假/KPI、概览 | 35 / +4126 |

三个前端共用一套骨架结构：`src/api`（类型化 API 封装）、`src/utils/http`（带 token 的 axios 实例）、
`src/services/oidcService`（OIDC 客户端）、`src/layouts/AppLayout`、`src/components/ui`（Modal / 状态标签）、
`src/router`、`src/views`。每个前端都带 Docker / nginx 配置与自己的 `package-lock.json`。

**功能亮点**
- OMS 渠道页直接对接上次交付的淘宝 adapter：`/api/channel-sync/adapters` 显示 `simulated` / `live` 模式，
  `POST /api/channel-sync/sync/{shopId}` 触发拉单并回显新建/跳过数量；还覆盖 SKU 映射与售后审批。
- FIN 凭证页支持一条凭证多分录编辑，并接"过账 / 作废"状态流转；期间页支持月结 / 反月结。
- HRM 薪资页覆盖算薪批次"开始 → 计算 → 核准发放"流转，另有请假余额与 KPI 录入。

### 2.2 配套改动

| 文件 | 改动 | 行数 |
|---|---|---:|
| `OctopusUMC/src/OctopusUMC.Api/Program.cs` | 补种缺失的 OIDC 客户端 `octopus-fin-web`（CORS 原已含 5181，但无客户端种子，FIN 前端无法登录） | +28 |
| `aspire/OctopusAspire.AppHost/Program.cs` | 新增 3 个 `AddViteApp` 注册（5181/5182/5183）+ 顶部端口注释 | +18 / -3 |
| `.github/workflows/ci.yml` | CI 前端矩阵新增 3 项 | +6 |
| `FEATURE_GAPS.md`（新建） | 24 条功能缺口需求清单，本次将 FR-01~03 标记完成 | +214 |
| `TODO.md` | 下一步清单新增"管理前端补齐"小节 | +6 |

> HRM（`octopus-hrm-web`）与 OMS（`octopus-oms-web`）的 OIDC 客户端种子原已存在，无需补种。

**合计**：113 个文件，+13112 / -3 行。

---

## 3. 验证结果

| 验证项 | 结果 |
|---|---|
| 三个前端 `npm run build`（vue-tsc + vite build） | ✅ 全通过 |
| UMC / AppHost 编译 | ✅ 通过 |
| FIN OIDC 客户端种子落库 | ✅ `octopus-fin-web` 已写入 `data/octopus_umc.db` |
| 浏览器实测 5181 / 5182 / 5183 | ✅ 页面正确渲染、标题正常、**0 控制台错误**、未登录自动跳 `/login` |
| 经 Vite proxy 调后端（模拟前端真实链路） | ✅ `/api` → 5007/5008/5009 全部 200（account / employee / catalog-shops / adapters） |
| OMS 适配器诊断 | ✅ 返回 `MOCK sandbox` + `TAOBAO simulated` |

构建时修复了骨架生成脚本的一处 bug：`AppLayout.vue` 中 groups 数组闭合符误写为 `] }},`，
已统一改为 `] },`（三个前端各一处）。

---

## 4. 排障记录

验证阶段三个前端曾一度全部返回 500，错误为 `execution attempt 不存在或已关闭`。
定位结论：

- 工具后台任务启动的 `restart.sh` 会让 Vite 子进程继承沙箱注入的
  `NODE_OPTIONS=--require=.../node-language-shim.cjs`；
- 后台任务会话断开后 broker IPC 关闭，Vite 内部请求即报 500（curl 与浏览器均复现，绕过代理无效）；
- **不是项目代码问题**，用 `env -u NODE_OPTIONS ./restart.sh` 重启后恢复；用户在终端前台启动不受影响。

另：浏览器内核偶发 `Target crashed`，清理残留进程后重开即可恢复。

---

## 5. package-lock 处置

- 本次工作区存在 **8 个历史遗留的 package-lock 改动**（CRM / MES / OA.H5 / OA.Web / Retail /
  UMC.H5 / UMC.Web / WMS），内容是删除 `@emnapi` 系列 optional 依赖，属有害改动 → **明确排除，未提交**。
- 三个新前端自带的 `package-lock.json` 属于新项目锁文件 → **正常包含**。
- 工具 / 诊断目录 `.loomy-attachments/`、`.playwright-cli/`、`.workbuddy/` 未提交。

提交后工作区仅剩上述 8 个历史 package-lock 改动，保持原样未动。

---

## 6. 后续

本次完成 FR-01~03。按 `FEATURE_GAPS.md` 排期，下一步建议：
1. **FR-10 / FR-11**：网关补 5 条路由（OA/PLM/UMC/OMS/HRM）+ PLM 补限流（小改动，可顺手做）；
2. **FR-09 短信发送**、京东 JOS adapter：受外部凭证制约，可先用仿真模式落框架；
3. **FR-04~08 OMS 中台规则引擎**：工作量最大，建议单独排期；
4. MySQL 迁移按既定约定压到最后。
