# Checkout Sale Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add checkout slice 2: live cart validation, confirmation, sale persistence, save-time conflict handling, and checkout reset.

**Architecture:** Keep `/caisse` as the Razor checkout page. Add thin resource-oriented API controllers under `/api/...` for validation and sale creation, with all business rules in `CheckoutService`.

**Tech Stack:** ASP.NET Core Razor Pages, ASP.NET Core controllers, EF Core SQLite, vanilla JavaScript, xUnit.

---

### Task 1: Service Validation And Save

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/CheckoutService.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/CheckoutCartTotals.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/CheckoutValidationResult.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/CheckoutSaleResult.cs`
- Modify: `tests/AssociationSaleTool.Web.Tests/Services/CheckoutServiceTests.cs`

**Steps:**
1. Write failing tests for valid cart validation, unavailable item conflict, sale persistence, sold status update, sale-line snapshots, and save-time conflict with no partial persistence.
2. Run `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter CheckoutServiceTests -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.artifacts\checkout-sale-service`.
3. Implement minimal `CheckoutService.ValidateCartAsync` and `CheckoutService.SaveSaleAsync`.
4. Rerun the filtered tests and commit `Add checkout sale service`.

### Task 2: Checkout API Endpoints

**Files:**
- Modify: `src/AssociationSaleTool.Web/Program.cs`
- Create: `src/AssociationSaleTool.Web/Controllers/CheckoutValidationController.cs`
- Create: `src/AssociationSaleTool.Web/Controllers/SalesController.cs`
- Create: `src/AssociationSaleTool.Web/Controllers/Models/CheckoutCartRequest.cs`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Steps:**
1. Write failing HTTP tests for `POST /api/caisse/validation` success/conflict and `POST /api/ventes` success/conflict.
2. Run the smoke tests with isolated output.
3. Add controller support in `Program.cs`, implement thin controllers returning `200`, `201`, `400`, or `409`.
4. Rerun smoke tests and commit `Add checkout sale API endpoints`.

### Task 3: Checkout Confirmation UI

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Checkout/Index.cshtml`
- Modify: `src/AssociationSaleTool.Web/wwwroot/js/site.js`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Steps:**
1. Write failing smoke/static tests for French `Finaliser`, modal hooks, API endpoint calls, conflict-row CSS class, and absence of print action.
2. Run the smoke tests with isolated output.
3. Add the finalization button and modal markup. Extend JS to validate, show modal, save, reload on success, and mark conflicts. Add CSS for modal and conflict rows.
4. Rerun smoke tests and commit `Add checkout sale confirmation UI`.

### Task 4: Full Verification

**Files:**
- No code changes expected.

**Steps:**
1. Run `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.artifacts\checkout-sale-full`.
2. Run `rtk git status --short`.
3. Confirm only known WAL and `.artifacts/` noise remains.
