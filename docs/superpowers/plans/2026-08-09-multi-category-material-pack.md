# PLM Multi-Category Material Pack Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically initialize reviewable standard material packs and BOM draft items for every supported active leaf category, with category filtering and batch review in the PLM material library.

**Architecture:** Extend the PLM material master with a many-to-many category binding and `pending_review` state. A focused idempotent initializer owns the built-in pack definitions and only fills empty V1.0 drafts; material APIs expose category-aware paging and audited batch review, while existing PLM release validators remain the final gate before MES.

**Tech Stack:** ASP.NET Core 10, Entity Framework Core 10, SQLite, xUnit, Vue 3, TypeScript, Axios, Vite.

## Global Constraints

- Preserve the five existing smartphone materials, published templates, manufacturing versions, MES snapshots, orders, and schedule plans.
- Initialization runs automatically at PLM startup and is safe to run repeatedly.
- New standard-pack materials use `pending_review`; they cannot enter a published template or released manufacturing version until reviewed to `active`.
- A material may bind to multiple categories, and `(MaterialId, CategoryId)` is unique.
- Only empty V1.0 drafts may be populated automatically; current, historical, and non-empty drafts are immutable to initialization.
- Material code/name search and category/type/status filtering remain server-paged with page size clamped to 100.
- Do not create suppliers, prices, inventory, warehouse batches, brand-specific specifications, or automatic releases.
- Preserve unrelated dirty workspace files and commit only task-scoped paths.

---

### Task 1: Material Category Binding Persistence

**Files:**
- Create: `OctopusPLM/src/OctopusPLM.Core/Entities/MaterialCategoryBinding.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Core/Entities/Material.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Core/Entities/Category.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Infrastructure/Persistence/PlmDbContext.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Infrastructure/Persistence/PlmDbSeeder.cs`
- Test: `OctopusPLM/tests/OctopusPLM.Api.Tests/MaterialCategoryBindingSchemaTests.cs`

**Interfaces:**
- Produces `MaterialCategoryBinding` with `BindingId`, `MaterialId`, `CategoryId`, `IsPrimary`, `Source`, and `CreatedAt`.
- Produces `DbSet<MaterialCategoryBinding> MaterialCategoryBindings` and SQLite self-healing for `plm_material_category_binding`.
- Extends the accepted material status domain to `pending_review | active | inactive` without changing existing rows.

- [ ] **Step 1: Write the failing schema test**

Create an in-memory SQLite context and assert the binding table exists, duplicate `(MaterialId, CategoryId)` fails, deleting a category with bindings is restricted, and a `pending_review` material persists unchanged.

```csharp
var material = new Material { MaterialCode = "GAR-SEW-THREAD", MaterialName = "通用缝纫线", Status = "pending_review" };
var category = new Category { Name = "T恤", Status = 1 };
db.MaterialCategoryBindings.Add(new() { Material = material, Category = category, IsPrimary = true, Source = "standard_pack" });
await db.SaveChangesAsync();
```

- [ ] **Step 2: Run the focused test and verify RED**

```bash
dotnet test OctopusPLM/tests/OctopusPLM.Api.Tests/OctopusPLM.Api.Tests.csproj --filter FullyQualifiedName~MaterialCategoryBindingSchemaTests
```

Expected: compile failure because `MaterialCategoryBinding` does not exist.

- [ ] **Step 3: Implement the entity and EF mapping**

Use restrictive foreign keys from binding to material/category, a unique index on `(MaterialId, CategoryId)`, an index on `CategoryId`, default `Source="manual"`, and navigation collections on both principals.

- [ ] **Step 4: Add SQLite compatibility self-healing**

Extend `PlmDbSeeder` using its existing model-derived table/index creation path. Verify existing databases gain the new table without rewriting `plm_material`, templates, or manufacturing rows.

- [ ] **Step 5: Run focused and full PLM tests**

Expected: the new schema test and all existing 35 PLM tests pass.

- [ ] **Step 6: Commit**

```bash
git add OctopusPLM/src/OctopusPLM.Core/Entities OctopusPLM/src/OctopusPLM.Infrastructure/Persistence OctopusPLM/tests/OctopusPLM.Api.Tests/MaterialCategoryBindingSchemaTests.cs
git commit -m "feat(plm): bind materials to categories"
```

### Task 2: Idempotent Standard Pack Initialization

**Files:**
- Create: `OctopusPLM/src/OctopusPLM.Api/Services/StandardMaterialPackCatalog.cs`
- Create: `OctopusPLM/src/OctopusPLM.Api/Services/StandardMaterialPackInitializer.cs`
- Create: `OctopusPLM/src/OctopusPLM.Api/Controllers/MaterialInitializationController.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Api/Program.cs`
- Test: `OctopusPLM/tests/OctopusPLM.Api.Tests/StandardMaterialPackInitializerTests.cs`

**Interfaces:**
- `StandardMaterialPackCatalog.All` exposes immutable category-name definitions with stable code, name, specification, unit, type, quantity, and loss rate.
- `Task<MaterialPackInitializationResult> InitializeAsync(long userId, string userName, CancellationToken ct)` returns category, created, reused, binding, template-fill, and skip counts plus per-category errors.
- Produces `POST /api/material-initialization/standard-packs` and `GET /api/material-initialization/last-result` for administrators.

- [ ] **Step 1: Write failing initializer tests**

Cover these concrete cases:

```csharp
result.CreatedMaterialCount.Should().BeGreaterThan(0);
second.CreatedMaterialCount.Should().Be(0);
db.Materials.Count(x => x.MaterialCode == "GAR-SEW-THREAD").Should().Be(1);
db.MaterialCategoryBindings.Count(x => x.Material!.MaterialCode == "GAR-SEW-THREAD").Should().Be(4);
phoneCurrent.UpdatedAt.Should().Be(originalPhoneUpdatedAt);
nonEmptyDraft.Items.Should().HaveCount(originalCount);
```

Also assert inactive and parent categories are skipped, all newly created rows are `pending_review`, an existing manually edited material is reused without field overwrite, and a failed category records an error without rolling back successful categories.

- [ ] **Step 2: Run focused tests and verify RED**

```bash
dotnet test OctopusPLM/tests/OctopusPLM.Api.Tests/OctopusPLM.Api.Tests.csproj --filter FullyQualifiedName~StandardMaterialPackInitializerTests
```

Expected: compile failure because the catalog and initializer do not exist.

- [ ] **Step 3: Implement the immutable catalog**

Define all nine category packs from the approved specification. Reuse these stable shared codes: `GAR-SEW-THREAD`, `GAR-MAIN-LABEL`, `GAR-PACK-BAG`, and `FOOD-OUTER-CARTON`. Use category-specific codes for all other materials and avoid supplier or brand attributes.

- [ ] **Step 4: Implement category-scoped initialization transactions**

For each active leaf category with a known pack: normalize codes, create missing materials as `pending_review`, preserve existing material fields/status, upsert bindings, then populate only an empty V1.0 draft. Append one `MaterialTemplateAudit` summary per category and store the last aggregate result in a singleton service or audit payload readable by the summary endpoint.

- [ ] **Step 5: Wire startup and safe failure handling**

Register the initializer and call it after existing category/template startup initialization. Catch per-category errors inside the initializer; log aggregate failures without preventing the PLM API from starting.

- [ ] **Step 6: Add administrator endpoints**

Apply `MaterialManage`; return project-standard `{ code, msg, data }`. The rerun endpoint calls the same idempotent service used at startup.

- [ ] **Step 7: Run focused/full tests and commit**

```bash
dotnet test OctopusPLM/tests/OctopusPLM.Api.Tests/OctopusPLM.Api.Tests.csproj --no-restore
git add OctopusPLM/src/OctopusPLM.Api/Services/StandardMaterialPackCatalog.cs OctopusPLM/src/OctopusPLM.Api/Services/StandardMaterialPackInitializer.cs OctopusPLM/src/OctopusPLM.Api/Controllers/MaterialInitializationController.cs OctopusPLM/src/OctopusPLM.Api/Program.cs OctopusPLM/tests/OctopusPLM.Api.Tests/StandardMaterialPackInitializerTests.cs
git commit -m "feat(plm): initialize category material packs"
```

### Task 3: Category-Aware Search and Batch Review API

**Files:**
- Modify: `OctopusPLM/src/OctopusPLM.Api/Services/MaterialService.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Api/Controllers/MaterialsController.cs`
- Test: `OctopusPLM/tests/OctopusPLM.Api.Tests/MaterialApiTests.cs`
- Test: `OctopusPLM/tests/OctopusPLM.Api.Tests/ManufacturingReleaseValidatorTests.cs`

**Interfaces:**
- Extends material search with `long? categoryId` and category summaries without changing `{ rows, total, page, pageSize }`.
- Produces `ReviewAsync(IReadOnlyCollection<long> materialIds, string targetStatus, long userId, string userName, CancellationToken ct)`.
- Produces `POST /api/materials/review` with `{ materialIds, targetStatus }` and result `{ successCount, failures }`.

- [ ] **Step 1: Extend API tests and verify RED**

Add tests proving category filter returns shared and category-specific material rows, `pending_review` is searchable but excluded by `selectableOnly=true`, batch review rejects anonymous/view-only users, duplicate IDs are processed once, missing IDs appear in failures, and successful state changes append audit rows.

Add release tests that assert error messages include the exact pending material code for template publication and manufacturing release.

- [ ] **Step 2: Implement category-aware paged projection**

Filter through `MaterialCategoryBindings.Any(x => x.CategoryId == categoryId)`. Project distinct category summaries ordered by name. Keep keyword matching on normalized code and name and clamp page size to 100.

- [ ] **Step 3: Implement audited batch review**

Accept only `active` or `inactive` targets. Deduplicate IDs, update found materials in one transaction, preserve missing IDs as per-item failures, and add an audit row per changed material containing old/new status.

- [ ] **Step 4: Improve release errors**

Resolve inactive/pending material codes in one query and return messages such as `物料 LAPTOP-MB-001 待审核，不能发布` rather than a generic incomplete-data message.

- [ ] **Step 5: Run PLM tests and commit**

```bash
dotnet test OctopusPLM/tests/OctopusPLM.Api.Tests/OctopusPLM.Api.Tests.csproj --no-restore
git add OctopusPLM/src/OctopusPLM.Api/Services/MaterialService.cs OctopusPLM/src/OctopusPLM.Api/Controllers/MaterialsController.cs OctopusPLM/tests/OctopusPLM.Api.Tests
git commit -m "feat(plm): review materials by category"
```

### Task 4: Multi-Category Material Library UI

**Files:**
- Modify: `OctopusPLM/web/OctopusPLM.Web/src/api/material/types.ts`
- Modify: `OctopusPLM/web/OctopusPLM.Web/src/api/material/index.ts`
- Modify: `OctopusPLM/web/OctopusPLM.Web/src/views/material/index.vue`

**Interfaces:**
- Adds `MaterialStatus = "pending_review" | "active" | "inactive"`, category summaries, category filter, type filter, selection, batch review, and initialization summary.
- Continues consuming paged material results and never downloads the full material catalog.

- [ ] **Step 1: Extend API types and clients**

Add `categories`, `categoryId`, `materialType`, `reviewMaterials`, `initializeStandardPacks`, and `getLastInitializationResult` types/functions. Keep `pageSize=20` for the table and existing `pageSize=30` for manufacturing remote search.

- [ ] **Step 2: Implement filters and category display**

Add native select controls for category, status, and type. Render category names as compact tags; show `待审核` in amber, `启用` in green, and `停用` in gray. Reuse the proven page-native controls rather than unregistered Element Plus template components.

- [ ] **Step 3: Implement selection and batch review**

Add a header checkbox, row checkboxes, selected count, `批量审核启用`, and `批量停用`. Require confirmation, display partial failures with material IDs, clear successful selections, and reload the current page.

- [ ] **Step 4: Show initialization summary and rerun action**

Display last run time and counts. Add `重新初始化标准物料包`; the result must state created/reused/bound/template-filled/skipped counts and never imply templates were published.

- [ ] **Step 5: Run typecheck/build and browser acceptance**

```bash
cd OctopusPLM/web/OctopusPLM.Web
npm run typecheck
npm run build
```

If full typecheck is still blocked only by the documented pre-existing `category/manage/index.vue` errors, record that exact baseline separately; the production build must exit 0. In the browser verify category filters, shared material category tags, batch enabling one category, and paging/search behavior.

- [ ] **Step 6: Commit**

```bash
git add OctopusPLM/web/OctopusPLM.Web/src/api/material OctopusPLM/web/OctopusPLM.Web/src/views/material/index.vue
git commit -m "feat(plm-web): manage category material packs"
```

### Task 5: End-to-End Review and Publication Acceptance

**Files:**
- Modify: `docs/acceptance/discrete-manufacturing-scheduling-acceptance-checklist.md`
- Modify: `docs/superpowers/plans/2026-08-09-multi-category-material-pack.md`

**Interfaces:**
- Produces reproducible acceptance evidence with actual material, binding, template, manufacturing, and MES IDs.

- [ ] **Step 1: Run final automated verification**

Run PLM and MES full tests, PLM and MES production builds, and `git diff --check`. Record exact totals and any known unrelated typecheck baseline.

- [ ] **Step 2: Restart and verify initialization**

Run `./restart.sh`. Query the local PLM database to prove all supported leaves have bindings, shared codes exist once, phone current/historical rows are unchanged, and a second initializer run creates zero material/binding/template rows.

- [ ] **Step 3: Exercise review and release protection**

In `/plm/material`, filter to one non-phone category, verify pending rows and category/code/name relationships, attempt template publication and capture the exact pending-code rejection, batch enable that category, then publish its populated V1.0 template.

- [ ] **Step 4: Exercise product and MES boundary**

Choose an existing active PLM product in the reviewed category, create a draft manufacturing version, apply the current category template, complete a valid routing, release it, and verify MES receives only that released version. Do not manufacture missing product-category relationships.

- [ ] **Step 5: Record evidence and commit**

Append actual counts, IDs, transitions, test totals, and browser results to the acceptance checklist. Mark this plan complete with any explicit out-of-scope items unchanged.

```bash
git add docs/acceptance/discrete-manufacturing-scheduling-acceptance-checklist.md docs/superpowers/plans/2026-08-09-multi-category-material-pack.md
git commit -m "docs: accept multi-category material packs"
```
