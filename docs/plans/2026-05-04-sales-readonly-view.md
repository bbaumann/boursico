# Read-Only Sales View Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add operator-visible, read-only sales browsing under `/caisse`.

**Architecture:** Add a small `SalesViewService` that reads immutable sale history through EF Core projections. Render `/caisse/ventes` and `/caisse/ventes/{id}` with Razor Pages; keep correction and mutation workflows on existing admin pages.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core SQLite, xUnit, existing CSS/vanilla JavaScript patterns.

---

### Task 1: Sales View Service Tests

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/SalesViewService.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/SalesViewModels.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/SalesViewServiceTests.cs`

**Step 1: Write failing tests**

Add tests for:

- `ListSalesAsync` returns newest sales first with item count, totals, receipt flag, and searchable text containing sale id, item reference, title, and receipt details.
- `GetSaleAsync` returns a sale detail with line references, titles, seller prices at sale time, and corrected-line timestamps.
- `GetSaleAsync` returns `null` when the sale does not exist.

**Step 2: Run tests to verify RED**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter SalesViewService -p:UseAppHost=false`

Expected: compile failure because the service/models do not exist.

**Step 3: Implement minimal service**

Add read-only EF projections from `Sales`, `SaleLines`, and `Items`, ordered by `SoldAt DESC, Id DESC`, with no tracking.

**Step 4: Run tests to verify GREEN**

Run the same filtered test command.

Expected: Sales view service tests pass.

### Task 2: Razor Pages And Navigation

**Files:**
- Create: `src/AssociationSaleTool.Web/Pages/Checkout/Sales/Index.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Checkout/Sales/Index.cshtml.cs`
- Create: `src/AssociationSaleTool.Web/Pages/Checkout/Sales/Details.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Checkout/Sales/Details.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Checkout/Index.cshtml`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Step 1: Write failing smoke/page tests**

Add tests proving:

- `/caisse/ventes` renders without admin unlock and contains `Liste des ventes`.
- `/caisse/ventes/{id}` renders an existing sale detail and labels corrected lines.

**Step 2: Run tests to verify RED**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter SalesView -p:UseAppHost=false`

Expected: fail because routes/pages do not exist.

**Step 3: Implement pages**

Add the list and detail Razor Pages with French operator-facing text. Add a `LISTE DES VENTES` button to the `/caisse` header. Keep all controls read-only.

**Step 4: Run tests to verify GREEN**

Run the same filtered test command.

Expected: sales view route tests pass.

### Task 3: Docs And Full Verification

**Files:**
- Modify: `docs/vnext-backlog.md`
- Modify: `docs/features.md`
- Modify: `docs/decisions.md`

**Step 1: Update docs**

Mark `View Existing Sales` as implemented, document `/caisse/ventes`, and record the access/route decision.

**Step 2: Run full verification**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false`

Expected: all tests pass.

**Step 3: Commit**

Commit the design, plan, code, tests, and docs with message `Add read-only sales view`.
