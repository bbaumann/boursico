# Defective Items Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Track accepted items that become defective, prevent their sale, return them to the seller, and record full buyer refunds when a sold item is returned as defective.

**Architecture:** Add `defective` as a first-class item status, record defective buyer refunds on `sale_line`, keep original sale totals immutable, and extend the existing admin sale-status page for both sold-line refunds and in-stock defect handling.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core, SQLite, xUnit.

---

## Task 1: Domain And Schema

- [x] Add failing tests:
  - `ItemStatusTransitionsTests` covers `in_stock -> defective` and blocks `defective -> sold`.
  - `ItemStatusDisplayTests` covers the French label `défectueux`.
  - `AppDbContextModelTests` covers `defective` in the item status constraint and refund columns on `sale_line`.
- [x] Run the focused tests and verify they fail for the missing status/schema.
- [x] Add `ItemStatus.Defective`.
- [x] Update transition and display helpers.
- [x] Extend `SaleLineEntity` with:
  - `RefundedAt`
  - `RefundItemPriceCents`
  - `RefundCheckoutFeeCents`
  - `RefundTotalCents`
  - `RefundReason`
- [x] Update `AppDbContext`:
  - map refund columns
  - update `ck_item_status`
  - add non-negative refund check constraints
  - replace the current active sale-line unique index with one filtered by `corrected_at IS NULL AND refunded_at IS NULL`
- [x] Add an EF migration and update the tracked SQLite DB.
- [x] Run the focused tests and verify they pass.

## Task 2: Checkout Guardrails

- [x] Add explicit `CheckoutServiceTests` for:
  - catalog excludes `defective` items
  - save-time validation rejects a `defective` item
- [x] Run the focused tests. Existing `in_stock`-only checkout guardrails already pass these tests, so no production change is required.
- [x] Update checkout queries/validation so only `in_stock` can be sold.
- [x] Run focused checkout tests and verify they pass.

## Task 3: Defect And Refund Services

- [x] Add failing `SaleCorrectionServiceTests` for:
  - `RefundDefectiveSaleLineAsync` records item price, association fee, total refund, reason, and `refunded_at`
  - refund changes the item from `sold` to `defective`
  - refunded lines are no longer listed as active sold items
  - corrected sale lines cannot be refunded
  - refunded sale lines cannot be corrected
- [x] Add failing tests for in-stock defect handling:
  - admin can mark `in_stock -> defective`
  - admin can restore `defective -> in_stock` when no refund history exists
  - admin cannot restore a defective item that has a refunded sale line
  - item search finds defect candidates by reference/title/seller context
- [x] Run the focused tests and verify they fail.
- [x] Extend `SaleCorrectionService` for sold-line defective refunds.
- [x] Add a small service for non-sold defect operations, or keep it in `SaleCorrectionService` if the local API remains cleaner that way.
- [x] Use `CheckoutPricingCalculator` for the refunded association fee so rounding matches checkout.
- [x] Reload current item/sale-line state immediately before saving status changes.
- [x] Run the focused tests and verify they pass.

## Task 4: Admin Page

- [x] Add/extend Razor Page tests if available for `/vente/statut`; otherwise add a smoke test that verifies rendered HTML contains:
  - `Correction des articles`
  - `Rembourser`
  - `Marquer défectueux`
  - a status column
  - no refund amount column
- [x] Run the focused tests and verify they fail.
- [x] Update `Pages/SaleStatus/Index.cshtml.cs`:
  - add `OnPostRefundDefectiveAsync`
  - add handlers for marking/restoring defective items
  - keep admin unlock behavior unchanged
  - use French success/error messages
- [x] Update `Pages/SaleStatus/Index.cshtml`:
  - show one item correction table for the first 15 non-`registered` items
  - show `Rembourser` beside `Remettre en stock` for sold items
  - show a confirmation pop-in with item reference, title, sale id, item price, refunded association fee, and total refund
  - show `Marquer défectueux` for `in_stock` items and `Remettre en stock` for eligible `defective` items
- [x] Keep the seller detail article list free of the admin-only `Marquer défectueux` action.
- [x] Run the focused page/smoke tests and verify they pass.

## Task 5: Reconciliation And Seller Sheets

- [x] Add failing `ReconciliationServiceTests` for:
  - sellers with only `defective` items are included in seller-sheet options
  - defective items appear in returned items
  - refunded sold lines are excluded from payable items
  - refunded defective items appear as returned items
- [x] Add/extend sheet render tests if available to verify the visible `DÉFECTUEUX` marker.
- [x] Run the focused tests and verify they fail.
- [x] Update reconciliation status sets and payable-line queries.
- [x] Update seller-sheet models and `Pages/Reconciliation/Sheets.cshtml` to mark defective returned items.
- [x] Run focused reconciliation tests and verify they pass.

## Task 6: Sales History

- [x] Add failing `SalesViewServiceTests` for refunded defective lines:
  - original sale totals remain unchanged
  - line exposes `RefundedAt` and a short refund status label
  - the sale exposes refund total and corrected total
  - corrected and refunded labels remain distinct
- [x] Run the focused tests and verify they fail.
- [x] Update `SalesViewService` and sales view models.
- [x] Update `Pages/Checkout/Sales/Details.cshtml` to show `Remboursé` with date in the item table, then `Remboursements` and `Total corrigé` in the totals block.
- [x] Run focused sales-history tests and verify they pass.

## Task 7: Documentation

- [x] Update canonical docs:
  - `docs/features.md`
  - `docs/item-status-lifecycle.md`
  - `docs/domain-model-and-references.md`
  - `docs/database-schema.md`
  - `docs/process-and-failover.md`
  - `docs/guide-utilisateur.md`
  - `docs/vnext-backlog.md`
- [x] Add a decision-log entry in `docs/decisions.md` for the sale-line refund model and seller-sheet treatment.
- [x] Check that user-facing French text has accents and natural wording.

## Task 8: Verification And Commit

- [x] Run `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false`.
- [x] Run `rtk git -c safe.directory=C:/dev/association-sale-tool/.worktrees/defective-items diff --check`.
- [x] Inspect staged/unstaged/untracked state and keep unrelated files out.
- [x] Commit from the worktree with a focused message.
