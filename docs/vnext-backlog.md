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

### Track Defective Items And Buyer Refunds

Status: implemented in the defective-items slice.

Relevant design/docs:

- [Defective Items Design](./plans/2026-05-05-defective-items-design.md)
- [Defective Items Implementation Plan](./superpowers/plans/2026-05-05-defective-items.md)
- [D-040 Track Defective Items And Refunds](./decisions.md#d-040-track-defective-items-and-refunds)

Implemented behavior:

- accepted defective items use status `defective`
- checkout excludes defective items
- admins can mark `in_stock -> defective` from `/administration`
- admins can restore `defective -> in_stock` only before any buyer refund exists
- buyer-return defects record refund fields on `sale_line`
- buyer refund amount includes item price plus the 10 percent association fee
- refunded defective items are returned to the seller and excluded from seller payout
- admins can cancel a defective refund entered by mistake, clearing the refund fields and restoring the item to `sold`

### View Existing Sales

Status: implemented.

Relevant design/docs:

- [Read-Only Sales View Design](./plans/2026-05-04-sales-readonly-view-design.md)
- [Read-Only Sales View Implementation Plan](./plans/2026-05-04-sales-readonly-view.md)
- [D-032 Expose Read-Only Sales History From Checkout](./decisions.md#d-032-expose-read-only-sales-history-from-checkout)

Saved sales are visible to all operators from `/caisse/ventes`, linked from the checkout page. The view is read-only, searchable by sale id, item reference, item title, and buyer address, and includes corrected sale lines as labeled historical facts. Sale-line and exceptional item correction stay on `/administration`.

## Item Intake

### Create Items From An Excel File

Status: implemented.

Relevant design/docs:

- [Excel Seller And Item Prefill Design](./plans/2026-05-04-excel-seller-item-prefill-design.md)
- [Excel Seller And Item Prefill Implementation Plan](./plans/2026-05-04-excel-seller-item-prefill.md)
- [D-039 Treat Seller Excel Files As Editable Intake Prefill](./decisions.md#d-039-treat-seller-excel-files-as-editable-intake-prefill)

Implemented behavior:

- Excel parsing is a form-prefill helper, not a direct import workflow
- `/vendeurs/creer` can upload one `.xlsx` workbook with one visible seller sheet; hidden helper sheets such as postal-code lists are ignored
- item rows are read from columns `B:C`, starting at the first template item row and continuing past the old 15-row limit until the first blank item row
- upload fills the editable seller and item form but saves nothing
- the normal save action creates the seller and item rows in one transaction
- duplicate sellers redirect to the existing seller page with the parsed item rows prefilled in the add-items form
- Excel-created seller prefill uses postal address `UNKNOWN` and paid seller fee `EUR 0` until those workbook fields are formally designed

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
