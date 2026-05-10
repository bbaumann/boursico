# Closing Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add checkout-code based read-only closing totals globally and by physical checkout.

**Architecture:** Store `sale.checkout_code` and `sale_line.refund_checkout_code` as simple text columns. Checkout captures the sale checkout code from browser `localStorage`; refund attribution uses an explicit admin dropdown populated from distinct sale checkout codes. `DataListService` computes closing rows for a new read-only `Clôture` tab under `/donnees`.

**Tech Stack:** ASP.NET Core Razor Pages, Entity Framework Core, SQLite, xUnit, vanilla JavaScript.

---

### Task 1: Persist Checkout Codes

**Files:**
- Modify: `src/AssociationSaleTool.Web/Data/Entities/SaleEntity.cs`
- Modify: `src/AssociationSaleTool.Web/Data/Entities/SaleLineEntity.cs`
- Modify: `src/AssociationSaleTool.Web/Data/AppDbContext.cs`
- Modify: `src/AssociationSaleTool.Web/Controllers/Models/CheckoutCartRequest.cs`
- Modify: `src/AssociationSaleTool.Web/Controllers/SalesController.cs`
- Modify: `src/AssociationSaleTool.Web/Services/CheckoutService.cs`
- Modify: `tests/AssociationSaleTool.Web.Tests/Services/CheckoutServiceTests.cs`
- Modify: `tests/AssociationSaleTool.Web.Tests/Data/AppDbContextModelTests.cs`

- [x] **Step 1: Write failing service/model tests**

Add tests asserting blank checkout codes are rejected, trimmed codes are stored on sales, and EF maps `CheckoutCode` to `checkout_code`.

- [x] **Step 2: Run the focused tests**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter "FullyQualifiedName~CheckoutServiceTests|FullyQualifiedName~AppDbContextModelTests"`

Expected: FAIL because checkout-code properties and validation do not exist yet.

- [x] **Step 3: Implement persistence and validation**

Add `CheckoutCode` to sale entities, request models, controller calls, and `CheckoutService.SaveSaleAsync`. Normalize with `Trim()` and reject blank input with a French validation message.

- [x] **Step 4: Run focused tests again**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter "FullyQualifiedName~CheckoutServiceTests|FullyQualifiedName~AppDbContextModelTests"`

Expected: PASS.

### Task 2: Record Refund Checkout Codes

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/SaleCorrectionService.cs`
- Modify: `src/AssociationSaleTool.Web/Services/Models/SaleCorrectionResult.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/SaleStatus/Index.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/SaleStatus/Index.cshtml`
- Modify: `tests/AssociationSaleTool.Web.Tests/Services/SaleCorrectionServiceTests.cs`

- [x] **Step 1: Write failing refund tests**

Add tests asserting refund checkout codes are required, stored after trimming, and cleared when a refund is canceled. Add a test that distinct checkout-code options are read from existing sales.

- [x] **Step 2: Run the focused tests**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter FullyQualifiedName~SaleCorrectionServiceTests`

Expected: FAIL because refund checkout code behavior does not exist yet.

- [x] **Step 3: Implement refund attribution**

Add `RefundCheckoutCode` to `SaleLineEntity`, map it in EF, add a required `refundCheckoutCode` parameter to `RefundDefectiveSaleLineAsync`, and populate the admin dialog dropdown from `ListCheckoutCodesAsync`.

- [x] **Step 4: Run focused tests again**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter FullyQualifiedName~SaleCorrectionServiceTests`

Expected: PASS.

### Task 3: Compute And Render Closing Rows

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/DataListService.cs`
- Modify: `src/AssociationSaleTool.Web/Services/Models/DataListModels.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Data/Index.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Data/Index.cshtml`
- Modify: `tests/AssociationSaleTool.Web.Tests/Services/DataListServiceTests.cs`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

- [x] **Step 1: Write failing closing tests**

Add a service test for global/per-checkout sales, refunds, and net totals. Add a smoke test that `/donnees?onglet=cloture` renders the `Clôture` tab and totals table.

- [x] **Step 2: Run focused tests**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter "FullyQualifiedName~DataListServiceTests|FullyQualifiedName~WebApplicationSmokeTests"`

Expected: FAIL because closing rows and tab rendering do not exist yet.

- [x] **Step 3: Implement the closing report**

Add `DataListClosingRow`, compute a global row and checkout rows in `DataListService`, add `cloture` tab normalization, and render a read-only table under `/donnees`.

- [x] **Step 4: Run focused tests again**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter "FullyQualifiedName~DataListServiceTests|FullyQualifiedName~WebApplicationSmokeTests"`

Expected: PASS.

### Task 4: Checkout Browser Setup

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Checkout/Index.cshtml`
- Modify: `src/AssociationSaleTool.Web/wwwroot/js/site.js`
- Modify: `tests/checkout-cheque-validation.test.mjs` or add `tests/checkout-station-code.test.mjs`

- [x] **Step 1: Write failing JavaScript test**

Add a node test asserting the checkout script uses `localStorage`, sends `checkoutCode`, and uses French setup text.

- [x] **Step 2: Run JavaScript tests**

Run: `rtk node --test tests/*.test.mjs`

Expected: FAIL because station-code setup is not implemented.

- [x] **Step 3: Implement setup**

Add a checkout setup block and JS that blocks checkout until a non-empty code is saved in `localStorage`, then includes `checkoutCode` in sale requests.

- [x] **Step 4: Run JavaScript tests again**

Run: `rtk node --test tests/*.test.mjs`

Expected: PASS.

### Task 5: Migrations And Docs

**Files:**
- Create: `src/AssociationSaleTool.Web/Migrations/<timestamp>_AddCheckoutClosingCodes.cs`
- Modify: `src/AssociationSaleTool.Web/Migrations/AppDbContextModelSnapshot.cs`
- Modify: `docs/vnext-backlog.md`
- Modify: `docs/features.md`
- Modify: `docs/database-schema.md`
- Modify: `docs/decisions.md`

- [x] **Step 1: Add migration**

Create an EF migration adding `sale.checkout_code` and `sale_line.refund_checkout_code`.

- [x] **Step 2: Update docs**

Document checkout-code storage, refund attribution, and the `Données > Clôture` report.

- [x] **Step 3: Run full verification**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false`

Run: `rtk node --test tests/*.test.mjs`

Expected: all tests pass.
