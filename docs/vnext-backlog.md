# VNext Backlog

This backlog captures known feature requests that are intentionally outside the current V1 commitment.

## Checkout And Item Lifecycle

### Prevent Editing Sold Items

Prevent operators from updating item master data after the item has been sold.

The future design should define:

- which item fields become read-only after sale persistence
- how the item edit page communicates that a sold item cannot be changed
- whether admins need an exceptional correction workflow
- how attempted updates are rejected server-side
- how this rule interacts with reconciliation and seller payout auditability

## Item Intake

### Create Items From An Excel File

Allow operators to import item rows from an Excel workbook instead of entering every row manually.

Accepted VNext design:

- [Excel Seller And Item Prefill Design](./plans/2026-05-04-excel-seller-item-prefill-design.md)
- [Excel Seller And Item Prefill Implementation Plan](./plans/2026-05-04-excel-seller-item-prefill.md)

The design treats Excel parsing as a form-prefill helper, not as a direct import workflow. Operators review the prefilled seller and item rows, then save through the normal intake form.

Implementation should cover:

- the accepted workbook format
- validation and error reporting before import
- how imported rows map to seller, title, price, and intake mode
- whether duplicate detection is required
- whether import is only an operator helper or a required workflow

## Seller Fee Management

### Implement Seller Fee Management And Tracking

Track seller-facing fees inside the application instead of handling them outside the system.

The future design should define:

- the seller fee rule, including the current EUR 2 per 15 items rule
- how fees are calculated, rounded, adjusted, and displayed
- whether fees are collected at intake, deducted from payout, or only reported
- audit expectations for fee payment or waiver tracking
- how seller sheets and reconciliation outputs include fee information
