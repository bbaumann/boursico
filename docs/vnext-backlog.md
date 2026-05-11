# VNext Backlog

V1 is feature-complete but not live yet. VNext means candidate work after the first live V1 event, not the current software state.

There are no required VNext items remaining for first live use. Earlier backlog slices that were described as VNext have been absorbed into the current V1 scope.

## Implemented In Current V1 From The Former Backlog

### Checkout And Item Lifecycle

- sold item master data is protected by sale phases and explicit sale-line correction
- saved sales are visible from `/caisse/ventes` and `/caisse/ventes/{id}` in read-only mode
- accepted defective items use status `defective`
- admins can mark `in_stock -> defective`, restore a defective item when no refund exists, refund a defective sold item, and cancel a mistaken defective refund
- buyer refunds include the item price plus the 10 percent association fee

Relevant docs:

- [Sale Phases And Sale Corrections](./sale-phases-and-corrections.md)
- [D-031 Use Event-Level Sale Phases And Explicit Sale-Line Corrections](./decisions.md#d-031-use-event-level-sale-phases-and-explicit-sale-line-corrections)
- [D-032 Expose Read-Only Sales History From Checkout](./decisions.md#d-032-expose-read-only-sales-history-from-checkout)
- [D-040 Track Defective Items And Refunds](./decisions.md#d-040-track-defective-items-and-refunds)

### Intake And Seller Management

- `/vendeurs/creer` can upload one reviewed `.xlsx` seller workbook as an editable form prefill
- item rows are read from columns `B:C`, including rows beyond the old 15-row template area
- Excel upload saves nothing until the operator reviews and submits the normal form
- duplicate seller detection blocks create/edit conflicts and redirects Excel-prefilled rows to the existing seller detail page for review
- Excel prefill reads `CP VILLE` when present and accepts only canonical postal-code/city pairs from the authoritative Isere city list
- `resources/depot-internet.xlsx` keeps its hidden `codes postaux` list generated from the same JSON file used by the application

Relevant docs:

- [Excel Seller And Item Prefill Design](./plans/2026-05-04-excel-seller-item-prefill-design.md)
- [D-039 Treat Seller Excel Files As Editable Intake Prefill](./decisions.md#d-039-treat-seller-excel-files-as-editable-intake-prefill)

### Seller Settlement

- expected seller fees are EUR 2 per started block of 15 seller items
- sellers store the manually paid amount as `fee_paid_cents`
- seller create/edit and detail screens show the paid amount
- seller sheets show expected, paid, remaining fee, and final payout after unpaid-fee deduction

Relevant docs:

- [D-037 Track Seller Registration Fees In-App](./decisions.md#d-037-track-seller-registration-fees-in-app)

### Payment And Data Inspection

- checkout persists sale-level payment method as card, cash, or cheque
- cheque sales require cheque number and bank name
- payment and cheque details are visible in read-only sales history
- `/donnees` exposes read-only `Articles`, `Vendeurs`, `Ventes`, and `Chèques` tabs for operator inspection

Relevant docs:

- [D-035 Persist Sale Payment Method And Cheque Traceability](./decisions.md#d-035-persist-sale-payment-method-and-cheque-traceability)
- [D-038 Expose Ungated Read-Only Data Lists](./decisions.md#d-038-expose-ungated-read-only-data-lists)

## Candidate Future VNext Ideas

These are optional post-V1 directions. They should stay out of the first live release unless a new decision makes one of them necessary.

### Seller Self-Service

A seller-facing intake flow could let sellers preregister themselves, enter items, and reduce organizer typing. The preferred VNext shape is not a hosted portal inside this application; the app should remain private on the local network.

The current feature request is a file-based form import workflow:

- sellers fill an external no-paid-third-party form, such as Google Forms or Framaforms/Yakforms
- organizers export responses as CSV, TSV, or spreadsheet files
- organizers upload the export into the local app
- the app shows an import preview and imports non-duplicate seller drafts with first name and last name
- missing or suspicious contact data is deferred to the seller deposit validation gate
- unreconciled city values are blanked on the seller and preserved in seller-level import notes
- duplicate submissions are not auto-imported; a single matched seller can receive valid new item lines, while ambiguous duplicates can only be ignored
- duplicate item lines on an existing seller are ignored when title and price match exactly
- invalid item lines are ignored rather than blocking seller import and remain visible through seller-level import notes

Relevant design:

- [Seller Self-Service Form Import](./seller-self-service-form-import.md)

### Broader Import And Export Tooling

V1 has a reviewed one-seller Excel prefill helper. Future work could support additional workbook shapes, batch review tooling, formal exports, archival packages, or provider-specific form-export mappings if real event operations need them.

### Richer Accounting And Refund Model

V1 tracks checkout totals, payment method, cheque details, seller fees, and defective buyer refunds. A future accounting model could add separate refund/payment ledger tables, payment-provider reconciliation, or richer seller accounting reports.

### Multi-Event History

V1 uses one database file per event. A future version could add multi-event browsing, archiving, or event selection if yearly operational history inside the app becomes valuable.

### Stronger Identity And Audit

V1 uses a shared admin password for risky operations. A future version could add named accounts, roles, and a stronger audit log if the operational cost becomes justified.
