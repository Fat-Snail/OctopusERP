# Dynamic Production Scheduling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed 2026-07-17 scheduling demo with a date-aware, API-backed production scheduling loop using real PLM-derived production orders, operations, resources, plans, publish state, and freeze state.

**Architecture:** Extend the existing MES scheduling service/controller with display-oriented schedule DTOs and a date query. Keep scheduling calculations in `SchedulingService`, persistence in EF Core entities, and orchestration in the controller. Refactor the Vue scheduling view so all task/resource/plan state comes from API responses and local dates are converted to ISO timestamps at the boundary.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core SQLite, xUnit/FluentAssertions, Vue 3 Composition API, TypeScript, Axios, Vite.

## Global Constraints

- Default to the user's local current date and schedule forward from the current time.
- For a future selected date, schedule from that date's available shift start.
- Store times as UTC and exchange timezone-aware ISO 8601 timestamps.
- Use only MES data synchronized from PLM published manufacturing versions; never restore demo tasks as fallback.
- Support one selected production order per simulation in this version.
- A plan with zero scheduled slots cannot be published.
- Preserve unrelated dirty worktree changes.

---

### Task 1: Return display-ready simulation results

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Services/Scheduling/SchedulingEngine.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Controllers/SchedulingController.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/SchedulingMasterDataTests.cs`

**Interfaces:**
- Consumes: `SchedulingRequest(long ProductionOrderId, DateTime From, DateTime To, decimal? Quantity = null)`.
- Produces: simulation slots containing task code/name, resource code/name, start/end, plus existing unscheduled/conflict data.

- [x] **Step 1: Write a failing service test**

Add a test that creates a production order, one operation task, a capability-matching resource and calendar, invokes `SimulateAsync`, and asserts the returned slot includes the literal task and resource display values.

- [x] **Step 2: Run the focused test and verify RED**

Run: `dotnet test OctopusMES/tests/OctopusMES.Api.Tests/OctopusMES.Api.Tests.csproj --filter SchedulingSimulation_ReturnsTaskAndResourceDisplayFields --no-restore`

Expected: compilation or assertion failure because `CandidateSlot` does not expose those fields.

- [x] **Step 3: Implement the minimal DTO enrichment**

Extend the simulation slot contract and map each selected task/resource into it without adding frontend-specific formatting.

- [x] **Step 4: Run the focused test and verify GREEN**

Run the same focused command; expected: 1 passing test.

- [x] **Step 5: Commit the backend simulation contract**

Stage only the task files and commit with `feat(mes): return display-ready schedule slots`.

### Task 2: Persist and query real plans by local date window

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Controllers/SchedulingController.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/Entities.cs` only if navigation/display projection requires it
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/SchedulingPlanApiTests.cs`

**Interfaces:**
- Produces: `GET /api/scheduling/plans?from=<iso>&to=<iso>` returning plans whose slots overlap `[from,to)` with order, task and resource display fields.
- Preserves: `POST /api/scheduling/plans`, `POST /plans/{id}/publish`, `POST /plans/{id}/freeze`, and `GET /plans/{id}`.

- [x] **Step 1: Write failing API tests**

Cover: overlap query includes a plan crossing the selected day, excludes a plan outside it, plan creation returns slots, publishing an empty plan is rejected, and published/frozen state survives a detail reload.

- [x] **Step 2: Run the new test class and verify RED**

Run: `dotnet test OctopusMES/tests/OctopusMES.Api.Tests/OctopusMES.Api.Tests.csproj --filter SchedulingPlanApiTests --no-restore`

Expected: failures because date listing and complete plan DTOs do not exist and empty publish is currently accepted.

- [x] **Step 3: Implement date query and plan projections**

Use EF Core includes/projections for plan → order, slots → operation task/resource. Validate `to > from`; select slots with `StartAt < to && EndAt > from`; return business envelopes with `code/msg/data`.

- [x] **Step 4: Implement publish/freeze validation**

Reject publish when no slots exist. Preserve the existing allowed status transitions and UTC timestamps.

- [x] **Step 5: Run focused and full backend tests**

Run the focused test class, then `dotnet test OctopusMES/tests/OctopusMES.Api.Tests/OctopusMES.Api.Tests.csproj --no-restore`; expected: all tests pass.

- [x] **Step 6: Commit plan persistence/query behavior**

Stage only the task files and commit with `feat(mes): query and persist production schedules`.

### Task 3: Replace fixed Vue demo state with the API workflow

**Files:**
- Create: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/schedulingTime.ts`
- Create: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/schedulingTime.test.ts` if the current frontend test runner supports unit tests; otherwise verify the pure functions through type-check/build and backend API tests
- Modify: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/SchedulingBoardView.vue`
- Modify: `OctopusMES/web/OctopusMES.Web/package.json` only if an existing test command can be enabled without a new framework

**Interfaces:**
- Produces: `todayLocalDate(): string`, `buildSchedulingWindow(date: string, now?: Date): { from: string; to: string }`, and date/time formatting helpers.
- Consumes: `/api/production-orders`, `/api/scheduling/simulate`, `/api/scheduling/plans`, `/publish`, and `/freeze`.

- [x] **Step 1: Write a failing pure time-window test where supported**

Assert with fixed `now = 2026-07-26T10:23:00+08:00` that today's window starts at the current minute and a future date starts at local midnight pending backend calendar adjustment; both outputs must be timezone-aware ISO values and span at most seven days.

- [x] **Step 2: Verify RED**

Run the existing frontend test command if present; otherwise run `npm run build` and verify failure while the imported helpers do not exist.

- [x] **Step 3: Implement time helpers and API-backed state**

Remove fixed `dayStart`, `tasks`, `resources`, `unscheduled`, plan number, `07-17` title and local-only button handlers. Add selected date, current production order/plan IDs, loading states, API error extraction, slot-to-Gantt mapping, date plan loading, and real simulate/create-plan/publish/freeze calls.

- [x] **Step 4: Add truthful empty and error states**

When no plan exists, show “该日期暂无真实排程计划”. When no current order exists, disable simulation with “请先创建生产订单”. Display backend `msg` for business errors and a service error for failed HTTP requests.

- [x] **Step 5: Verify frontend compilation**

Run: `npm run build` in `OctopusMES/web/OctopusMES.Web`; expected: Vue type-check and Vite build exit 0.

- [x] **Step 6: Commit the dynamic scheduling UI**

Stage only the scheduling frontend files and commit with `feat(mes-web): show dynamic production schedules`.

### Task 4: Live closed-loop verification

**Files:**
- Modify: `docs/acceptance/discrete-manufacturing-scheduling.md` if the existing acceptance document matches this feature; otherwise create `docs/acceptance/dynamic-production-scheduling.md`

**Interfaces:**
- Verifies the complete PLM → MES order → operations → simulation → published plan → frozen plan flow.

- [x] **Step 1: Restart the local stack**

Run `bash restart.sh` and wait for `http://127.0.0.1:5006/health` and `http://127.0.0.1:5178/scheduling` to respond.

- [x] **Step 2: Execute the real browser flow**

Using a PLM product with a published manufacturing version: create the order, confirm the operation count, simulate on the current date, verify task blocks use real operation/resource names, publish, reload, and freeze.

- [x] **Step 3: Verify persistence directly**

Query `mes_production_order`, `mes_operation_task`, `mes_schedule_plan`, and `mes_schedule_slot` in `data/octopus_mes.db`; record the generated IDs/statuses in the acceptance document without inserting synthetic rows.

- [x] **Step 4: Run final verification**

Run the full MES backend tests, MES frontend build, health checks, and `git diff --check`. Expected: zero test failures, build exit 0, healthy services, no whitespace errors.

- [x] **Step 5: Commit acceptance evidence**

Stage only the acceptance document and commit with `docs: record dynamic scheduling acceptance`.
