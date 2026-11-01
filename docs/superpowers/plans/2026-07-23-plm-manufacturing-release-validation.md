# PLM Manufacturing Release Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 PLM 制造版本列表和编辑区用红色准确标出缺失制造资料，并阻止不完整版本发布到 MES。

**Architecture:** 新增无状态的制造版本校验器，后端列表、详情与发布接口共享同一套结构化校验结果。前端使用后端返回的 `releaseReady` 和 `validationIssues` 展示列表摘要，并在草稿编辑时按字段路径显示红色错误。

**Tech Stack:** ASP.NET Core 10、EF Core/SQLite、xUnit/FluentAssertions、Vue 3、TypeScript、Element Plus、Vite。

## Global Constraints

- 所有商品、BOM、工艺路线数据必须来自用户维护的真实 PLM 数据，不生成默认业务数据。
- 草稿允许保存不完整资料，但不完整资料不得发布或通知 MES。
- 字段错误必须在输入框附近以红色边框和红色文字持续显示，不能只依赖短暂消息。
- 后端校验是最终发布边界，前端校验只用于即时反馈。

---

### Task 1: Shared release validator

**Files:**
- Create: `OctopusPLM/src/OctopusPLM.Api/Services/ManufacturingReleaseValidator.cs`
- Create: `OctopusPLM/tests/OctopusPLM.Api.Tests/ManufacturingReleaseValidatorTests.cs`

**Interfaces:**
- Produces: `ManufacturingValidationIssue(string Path, string Message)`、`ManufacturingReleaseValidator.Validate(ManufacturingVersion): IReadOnlyList<ManufacturingValidationIssue>`。

- [x] **Step 1: Write failing validator tests** covering empty BOM, empty material code/name, invalid quantity/loss rate, empty routing, empty operation code/name/capability, invalid cycle/setup time, and a valid version.
- [x] **Step 2: Run** `dotnet test OctopusPLM/tests/OctopusPLM.Api.Tests/OctopusPLM.Api.Tests.csproj --filter ManufacturingReleaseValidatorTests` and verify failures because the validator does not exist.
- [x] **Step 3: Implement validator** returning paths such as `bomItems[0].materialCode` and `operations[0].requiredCapability` with Chinese messages.
- [x] **Step 4: Re-run the filtered tests** and require zero failures.

### Task 2: API exposes and enforces structured validation

**Files:**
- Modify: `OctopusPLM/src/OctopusPLM.Api/Controllers/ManufacturingController.cs`
- Create: `OctopusPLM/tests/OctopusPLM.Api.Tests/ManufacturingReleaseApiTests.cs`

**Interfaces:**
- Consumes: `ManufacturingReleaseValidator.Validate`.
- Produces: list/detail fields `releaseReady: bool` and `validationIssues: [{ path, message }]`; release failure response `code: 422` with the same issues.

- [x] **Step 1: Write failing API tests** proving incomplete list/detail responses expose red-field-compatible paths and release returns code 422 without changing `Status` or creating a MES notification.
- [x] **Step 2: Run filtered API tests** and verify expected failures.
- [x] **Step 3: Update list/detail projection** to compute validation after loading entities and return structured issues.
- [x] **Step 4: Replace ad-hoc release checks** with the shared validator and return `422` plus issues before changing status.
- [x] **Step 5: Run filtered API tests** and require zero failures.

### Task 3: Frontend red validation and action states

**Files:**
- Create: `OctopusPLM/web/OctopusPLM.Web/src/views/manufacturing/validation.ts`
- Modify: `OctopusPLM/web/OctopusPLM.Web/src/views/manufacturing/index.vue`

**Interfaces:**
- Consumes: `VersionRow.releaseReady` and `VersionRow.validationIssues`.
- Produces: `issueFor(path)`, `hasIssue(path)`, list summary, red field styles, and “补齐发布资料” action.

- [x] **Step 1: Add a failing static regression check** asserting the component contains persistent issue rendering and does not rely only on `ElMessage`.
- [x] **Step 2: Add validation types/helpers** for exact field-path matching and issue summaries.
- [x] **Step 3: Render list state** as red “资料不完整” with red missing-item summary; change action to “补齐发布资料”.
- [x] **Step 4: Render field state** with red border and red message below each invalid BOM/operation input.
- [x] **Step 5: Update release flow** to use returned issues, open the editor when blocked, and preserve explicit login/network error messages.
- [x] **Step 6: Run** `npm run build` and the regression check; require success.

### Task 4: Full verification and runtime handoff

**Files:**
- Modify only if verification reveals a scoped defect.

- [x] **Step 1: Run** `dotnet test OctopusPLM/tests/OctopusPLM.Api.Tests/OctopusPLM.Api.Tests.csproj --no-restore --logger 'console;verbosity=minimal'`.
- [x] **Step 2: Run** `npm run build` in `OctopusPLM/web/OctopusPLM.Web`.
- [x] **Step 3: Restart local Aspire services** without resetting persistent data.
- [x] **Step 4: Verify** ports 5003/5006/5175/5178 and confirm the served manufacturing component contains the new red validation copy.
- [x] **Step 5: Query SQLite** to confirm the existing incomplete V1.0 remains draft and no MES event is created until the user supplies real data.
