# VNext Backlog

This backlog captures known feature requests that are intentionally outside the current V1 commitment.

## Checkout And Item Lifecycle

### Prevent Editing Sold Items

Prevent operators from updating item master data after the item has been sold.

Status: covered by the sale phase and sale correction work that is currently under verification.

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

Once the sale phase work is verified and committed, this item can be treated as implemented rather than a separate VNext feature.

### View Existing Sales

Allow admins to find and inspect an already-saved sale in read-only mode.

The first lookup path should support searching by item reference/code, because an operator or admin will often start from a physical label or a disputed item.

The future design should define:

- which admin page exposes sale lookup, for example `/vente/statut` or a dedicated French route such as `/ventes`
- whether lookup searches only active sale lines or also corrected sale lines
- what happens when an item appears in multiple historical sale lines because one line was corrected
- how the read-only sale view reuses the existing checkout/cart layout without allowing edits, removals, or save actions
- which sale fields are shown: sale id, sold timestamp, item references, item titles, seller prices at sale time, subtotal, checkout fee, total paid, and receipt details
- whether the page should show correction metadata such as `corrected_at`
- whether access is limited to admins through the existing shared admin password/session gate
- whether the view includes a direct action to start a sale-line correction when the admin discovers that the sale contains the wrong item

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
- when a submitted phone is present, the trimmed phone must also match
- when a submitted phone is blank, same-name matches block and ask the operator to verify manually
- all duplicate matches are shown with seller code, contact context, item count, and a link to the existing seller
- existing historical duplicates are not cleaned up by this feature
- future Excel prefill should reuse the same duplicate-check service and redirect to the existing seller with pending item rows

## Seller Fee Management

### Implement Seller Fee Management And Tracking

Track seller-facing fees inside the application instead of handling them outside the system.

The future design should define:

- the seller fee rule, including the current EUR 2 per 15 items rule
- how fees are calculated, rounded, adjusted, and displayed
- whether fees are collected at intake, deducted from payout, or only reported
- audit expectations for fee payment or waiver tracking
- how seller sheets and reconciliation outputs include fee information
