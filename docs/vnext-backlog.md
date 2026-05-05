# VNext Backlog

V1 is complete. This backlog captures VNext feature requests and implementation slices that extend the completed V1 baseline.

## Checkout And Item Lifecycle

### Prevent Editing Sold Items

Prevent operators from updating item master data after the item has been sold.

Status: implemented by the sale phase and sale correction work.

Relevant design/docs:

- [Sale Phases And Sale Corrections](./sale-phases-and-corrections.md)
- [D-031 Use Event-Level Sale Phases And Explicit Sale-Line Corrections](./decisions.md#d-031-use-event-level-sale-phases-and-explicit-sale-line-corrections)

The sale phase design covers the original backlog questions:

- item master data is read-only in `sale` and `post_sale`
- sold items are not editable through normal intake operations, even if the event is still in `presale`
- the item edit page and seller detail page communicate the read-only state in French
- server-side intake operations reject item master-data updates when the item is sold or the event phase locks item editing
- mistaken sold-item corrections use an explicit admin sale-line correction instead of an ad hoc `sold -> in_stock` item status edit
- seller sheets ignore corrected sale lines and use current item status for the returned item, preserving seller payout consistency

This item is implemented rather than a separate open VNext feature.

### View Existing Sales

Status: implemented.

Relevant design/docs:

- [Read-Only Sales View Design](./plans/2026-05-04-sales-readonly-view-design.md)
- [Read-Only Sales View Implementation Plan](./plans/2026-05-04-sales-readonly-view.md)
- [D-032 Expose Read-Only Sales History From Checkout](./decisions.md#d-032-expose-read-only-sales-history-from-checkout)

Saved sales are visible to all operators from `/caisse/ventes`, linked from the checkout page. The view is read-only, searchable by sale id, item reference, item title, and buyer address, and includes corrected sale lines as labeled historical facts. Sale-line correction stays on `/vente/statut`.

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

## Seller Management

### Prevent Duplicate Sellers

Prevent operators from accidentally creating a second seller when another seller already exists with the same identifying information.

Status: implemented by the seller duplicate prevention slice.

Relevant design:

- [Seller Duplicate Prevention Design](./superpowers/specs/2026-05-04-seller-duplicate-prevention-design.md)

Implemented behavior:

- create and edit both block exact normalized duplicate sellers
- matching uses normalized first name and last name, with accent-insensitive comparison
- phone is mandatory, and the trimmed phone must also match
- all duplicate matches are shown with seller code, contact context, item count, and a link to the existing seller
- existing historical duplicates are not cleaned up by this feature
- future Excel prefill should reuse the same duplicate-check service and redirect to the existing seller with pending item rows

## Seller Fee Management

### Implement Seller Fee Management And Tracking

Status: implemented.

Relevant design/docs:

- [Seller Fees Design](./plans/2026-05-04-seller-fees-design.md)
- [Seller Fees Implementation Plan](./plans/2026-05-04-seller-fees.md)
- [D-037 Track Seller Registration Fees In-App](./decisions.md#d-037-track-seller-registration-fees-in-app)

Implemented behavior:

- expected fees are EUR 2 per started block of 15 seller items
- sellers store the manually paid amount as `fee_paid_cents`
- seller details display expected and paid fee status
- seller create/edit screens accept the paid fee amount in whole euros
- seller sheets deduct unpaid fees from final payout
