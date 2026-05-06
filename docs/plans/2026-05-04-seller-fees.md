# Seller Fees Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add current V1 seller fee tracking, display, and reconciliation payout deduction.

**Architecture:** Store only the manually paid seller fee on `seller` as integer cents. Compute the expected fee from the seller's total item count with a small domain calculator, then project fee status into seller intake pages and reconciliation seller sheets.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core SQLite migrations, xUnit, existing Razor/CSS patterns.

---

### Task 1: Seller Fee Domain And Schema

**Files:**
- Create: `src/AssociationSaleTool.Web/Domain/Sellers/SellerFeeCalculator.cs`
- Modify: `src/AssociationSaleTool.Web/Data/Entities/SellerEntity.cs`
- Modify: `src/AssociationSaleTool.Web/Data/AppDbContext.cs`
- Create: `src/AssociationSaleTool.Web/Migrations/<timestamp>_AddSellerFeePaid.cs`
- Modify: `src/AssociationSaleTool.Web/Migrations/AppDbContextModelSnapshot.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Domain/SellerFeeCalculatorTests.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Data/AppDbContextModelTests.cs`

**Step 1: Write failing tests**

Add tests proving:

- zero items owes `0`
- 1, 15 items owe `200`
- 16, 30 items owe `400`
- 31 items owe `600`
- paid amount greater than expected leaves remaining fee at `0`
- the EF model maps required `SellerEntity.FeePaidCents` to `seller.fee_paid_cents`

**Step 2: Run tests to verify RED**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter "SellerFeeCalculator|SellerStoresFeePaidColumn" -p:UseAppHost=false`

Expected: compile/test failure because the calculator and mapped property do not exist.

**Step 3: Implement minimal domain and schema**

Add the calculator, the `FeePaidCents` property, EF mapping, a non-negative check constraint, and a migration that adds `fee_paid_cents INTEGER NOT NULL DEFAULT 0`.

**Step 4: Run tests to verify GREEN**

Run the same filtered test command.

Expected: targeted domain/schema tests pass.

### Task 2: Seller Intake Fee Entry And Display

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/Models/CreateSellerRequest.cs`
- Modify: `src/AssociationSaleTool.Web/Services/Models/UpdateSellerRequest.cs`
- Modify: `src/AssociationSaleTool.Web/Services/IntakeService.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`
- Test: `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Step 1: Write failing tests**

Add tests proving:

- seller create defaults paid fee to zero when omitted
- seller edit persists `Frais payes`
- negative paid fee is rejected with a French validation message
- seller details render the expected/paid fee status using the seller's item count

**Step 2: Run tests to verify RED**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter "SellerEditPage_Post_UpdatesSellerFeePaid|SellerDetailsPage_RendersSellerFeeStatus|SellerFeePaid" -p:UseAppHost=false`

Expected: fail because seller fee input/display behavior does not exist.

**Step 3: Implement minimal intake changes**

Parse paid fee as whole euros, store cents, and render the status near seller contact details. Keep the wording French:

- `Frais`
- `payés`
- `Frais payés`

**Step 4: Run tests to verify GREEN**

Run the same filtered test command.

Expected: targeted seller intake tests pass.

### Task 3: Reconciliation Fee Deduction

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/Models/SellerSheet.cs`
- Modify: `src/AssociationSaleTool.Web/Services/ReconciliationService.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Reconciliation/Sheets.cshtml`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/ReconciliationServiceTests.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Step 1: Write failing tests**

Add tests proving:

- seller sheets expose item subtotal, expected fee, paid fee, remaining fee, and final payout
- unpaid fees are subtracted from payout
- overpaid fees do not increase payout
- printable sheets render French fee rows

**Step 2: Run tests to verify RED**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter "ReconciliationSellerFee|SellerSheet" -p:UseAppHost=false`

Expected: fail because seller sheet fee fields and payout deduction do not exist.

**Step 3: Implement minimal reconciliation changes**

Use seller total item count for expected fee and existing payable item logic for item subtotal. Compute final payout as `max(item_subtotal - remaining_fee, 0)`.

**Step 4: Run tests to verify GREEN**

Run the same filtered test command.

Expected: targeted reconciliation tests pass.

### Task 4: Docs, Full Verification, And Commit

**Files:**
- Modify: `docs/decisions.md`
- Modify: `docs/features.md`
- Modify: `docs/database-schema.md`
- Modify: `docs/domain-model-and-references.md`
- Modify: `docs/vnext-backlog.md`

**Step 1: Update docs**

Add a current V1 seller-fee decision superseding `D-012`, update the database schema, update reconciliation behavior, and remove the seller-fee item from the VNext backlog.

**Step 2: Run full verification**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false`

Expected: all tests pass.

**Step 3: Commit**

Commit the design, plan, code, tests, and docs with message `Add seller fee tracking`.
