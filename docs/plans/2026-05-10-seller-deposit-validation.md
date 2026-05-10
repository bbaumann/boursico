# Seller Deposit Validation Implementation Plan

> **Execution note:** Use the plan task-by-task and keep verification checkpoints.

**Goal:** Replace manual item-level deposit transitions with a seller-level signed deposit validation workflow.

**Architecture:** Add one seller deposit status field and keep the paper contract as the legal proof. Normal intake creates registered items only; validating the seller deposit moves accepted registered items to stock and locks normal seller/item edits. Admin cancellation uses a local password confirmation, reopens the seller, moves only current stock items back to registered, and leaves sold, missing, and defective items read-only.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core SQLite migrations, xUnit service and smoke tests, existing French UI conventions.

---

### Task 1: Persist Seller Deposit Status

**Files:**
- Modify: `src/AssociationSaleTool.Web/Data/Entities/SellerEntity.cs`
- Modify: `src/AssociationSaleTool.Web/Data/AppDbContext.cs`
- Add: `src/AssociationSaleTool.Web/Migrations/20260510120000_AddSellerDepositStatus.cs`
- Modify: `src/AssociationSaleTool.Web/Migrations/AppDbContextModelSnapshot.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Data/AppDbContextModelTests.cs`

**Steps:**
1. Write a failing model test proving `seller.deposit_status` is required, defaults to `deposit_pending`, and is constrained to `deposit_pending` or `deposit_validated`.
2. Add `SellerEntity.DepositStatus`.
3. Map the column and check constraint.
4. Add the migration and update the model snapshot.
5. Run the focused model tests.

### Task 2: Move Stock Transition Behind Seller Validation

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/IntakeService.cs`
- Add: `src/AssociationSaleTool.Web/Services/Models/SellerDepositStatusChangeResult.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`

**Steps:**
1. Write failing tests proving new items are always registered, seller validation moves registered items to stock, validated sellers cannot be edited through normal intake, and admin cancellation reopens current stock items while tolerating historical sold, missing, and defective items.
2. Implement validation and cancellation service methods.
3. Make seller/item edit checks depend on seller deposit status.
4. Keep validation and cancellation allowed only while item master data is editable.
5. Run the focused intake service tests.

### Task 3: Gate Checkout On Validated Sellers

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/CheckoutService.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/CheckoutServiceTests.cs`

**Steps:**
1. Write failing tests proving pending-seller stock items are hidden from checkout catalog and rejected at save-time validation.
2. Filter catalog items by `seller.deposit_status = deposit_validated`.
3. Include seller status in cart validation conflict detection.
4. Run the focused checkout service tests.

### Task 4: Seller Detail UI And Contract Print Page

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml.cs`
- Add: `src/AssociationSaleTool.Web/Pages/Sellers/DepositContract.cshtml`
- Add: `src/AssociationSaleTool.Web/Pages/Sellers/DepositContract.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Steps:**
1. Write failing smoke tests for the seller status block, `Imprimer le contrat`, validation dialog, absence of `Dépôt` entry mode, print contract page, read-only reason, and admin cancellation behavior.
2. Remove the intake mode choice from normal seller item entry.
3. Add a seller-detail status block and contract actions.
4. Implement `/vendeurs/{id}/contrat-depot` as a print-focused French contract recap with association identity, seller fee status, legal mentions, current item prices, seller-only signature, and hidden app sale status while printing.
5. Use a confirmation dialog for `Valider le dépôt` with actions `Le contrat est signé` and `Retour`.
6. Show admin cancellation during presale for validated sellers and require the shared admin password in a local confirmation dialog.

### Task 5: Update Canonical Docs

**Files:**
- Modify: `docs/features.md`
- Modify: `docs/scope-and-phasing.md`
- Modify: `docs/process-and-failover.md`
- Modify: `docs/item-status-lifecycle.md`
- Modify: `docs/domain-model-and-references.md`
- Modify: `docs/database-schema.md`
- Modify: `docs/decisions.md`
- Modify: `docs/guide-utilisateur.md`

**Steps:**
1. Record the new seller deposit validation decision.
2. Replace the old operator-facing `Dépôt` item-entry mode with seller-level deposit validation.
3. Document the paper contract as the legal proof and the database status as operational state only.

### Task 6: Final Verification

**Commands:**
- `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false`
- `rtk node --check src\AssociationSaleTool.Web\wwwroot\js\site.js`
- `rtk git diff --check`

**Expected:** all commands exit successfully with no test failures.
