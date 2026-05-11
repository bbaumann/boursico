# Database Schema

## Purpose

This document defines the current V1 logical database schema for the association toy sale tool. V1 is feature-complete but not live yet; seller fees and defective-item refunds are part of the current V1 schema.

V1 runs on SQLite on the central server.

V1 uses one database file per event.

Static event metadata such as the event name or year code lives in application configuration rather than in relational tables.

Mutable operational event state, such as the current sale phase, lives in the database so every station follows the same server-side rules.

The schema is designed for a minimal normalized relational model.

Concrete DDL drafts are available in:

- [SQLite DDL](./sql/sqlite.sql)
- [DDL Notes](./sql/README.md)

## Design Principles

- One physical toy equals one item record.
- One completed checkout equals one sale.
- One active sold toy appears in exactly one active payable sale line.
- Corrected sale lines remain as historical correction evidence.
- Refunded sale lines remain as historical refund evidence.
- Seller sheets are derived from current data, not stored as their own primary business objects in V1.
- One database file represents one event operationally in V1.

## Tables

## `seller`

Represents one seller.

### Columns

- `id`
- `seller_code`
- `first_name`
- `last_name`
- `email`
- `phone`
- `postal_address`
- `postal_code`
- `city`
- `fee_paid_cents`
- `deposit_status`
- `notes`

### Notes

- `seller_code` is the two-letter safe code used in item references.
- No stored `display_name` is required; it can be derived when needed.
- `fee_paid_cents` stores the manually collected seller registration fee amount and defaults to `0`.
- The expected seller fee is derived from the seller's item count and is not stored. `registered` items count because they remain seller items in the system.
- `deposit_status` stores the operational seller deposit gate and defaults to `deposit_pending`.
- Phone, postal address, postal code, and city may be blank while `deposit_status = deposit_pending`; the application requires them before deposit validation.
- The signed paper deposit contract is the legal proof of deposit and price agreement; V1 does not store a contract number, signed timestamp, or contract snapshot.

## `item`

Represents one physical toy.

### Columns

- `id`
- `seller_id`
- `reference`
- `item_number`
- `title`
- `seller_price_cents`
- `status`

### Notes

- `reference` is the human-facing code, e.g. `BK014`.
- `item_number` is the seller-local internal sequence used to build the reference.
- `title` is the item label shown in search results, carts, and sheets.
- `seller_price_cents` is both the seller payout price and the printed per-item customer price in V1.
- money values are stored as integer cents, e.g. `250` for `2.50 EUR`.
- V1 operator entry accepts seller prices as whole euros only; new application-created `seller_price_cents` values should be multiples of `100`.
- The cents column is intentionally kept so the model can support cents later without a column rename.
- `status` uses the current status model: `registered`, `in_stock`, `sold`, `missing`, `defective`.

## `sale`

Represents one completed checkout.

### Columns

- `id`
- `sold_at`
- `subtotal_seller_price_cents`
- `checkout_fee_cents`
- `total_paid_cents`
- `buyer_name`
- `buyer_postal_code`
- `buyer_city`
- `buyer_address`
- `invoice_number`
- `invoice_issued_at`
- `payment_method`
- `cheque_number`
- `cheque_bank_name`

### Notes

- There is no `is_paid` field in V1; persisting the sale means the checkout is complete.
- `sold_at` should be stored in UTC.
- `subtotal_seller_price_cents`, `checkout_fee_cents`, and `total_paid_cents` are stored as integer cents.
- `buyer_name`, `buyer_postal_code`, and `buyer_city` are required by the application for new checkout sales.
- `buyer_postal_code` is stored as text even though the UI input is numeric, so postal-code formatting is not treated as arithmetic.
- `buyer_address` is an optional free-text customer address field used only when an invoice must be printed.
- `invoice_number` is null for normal sale saves and unique when an invoice was created.
- `invoice_issued_at` is null for normal sale saves and stores the server-side invoice issue timestamp when an invoice was created.
- Invoice numbers use the configured `YYMMAFBJNNNN` sequence, for example `2611AFBJ0001`.
- `payment_method` is set for new sales and uses `card`, `cash`, or `cheque`.
- `cheque_number` and `cheque_bank_name` are required by the application only when `payment_method` is `cheque`.
- Existing rows created before this feature may have null payment traceability fields.

## `sale_line`

Represents one sold item inside one sale.

### Columns

- `id`
- `sale_id`
- `item_id`
- `seller_price_at_sale_cents`
- `corrected_at`
- `refunded_at`
- `refund_item_price_cents`
- `refund_checkout_fee_cents`
- `refund_total_cents`
- `refund_reason`

### Notes

- `seller_price_at_sale_cents` is the per-item snapshot stored to protect reconciliation if item data is later edited by mistake.
- In V1, this snapshot is also the printed item price seen by the customer.
- Snapshot prices are stored as integer cents.
- `corrected_at` is null for an active sale line.
- If `corrected_at` is filled, the line was corrected by an admin and the linked item can return to `in_stock`.
- `refunded_at` is null for non-refunded sale lines.
- If `refunded_at` is filled, the buyer was refunded and the linked item is `defective`.
- `refund_item_price_cents` stores the item price reimbursed to the buyer.
- `refund_checkout_fee_cents` stores the reimbursed 10 percent association fee.
- `refund_total_cents = refund_item_price_cents + refund_checkout_fee_cents`.
- `refund_reason` is `defective` for the current refund workflow.
- Original sale totals are not recalculated when a sale line is corrected.
- Original sale totals are not recalculated when a sale line is refunded.
- If an admin cancels a defective refund that was entered by mistake, the refund fields are cleared and the original sale totals still remain unchanged.

## `event_state`

Represents mutable event-level operational state.

V1 uses a single row with `id = 1`.

### Columns

- `id`
- `sale_phase`
- `updated_at`

### Notes

- `sale_phase` values are `presale`, `sale`, and `post_sale`.
- a new database starts in `presale`.
- phase changes are made through `/administration` and require `Admin:SharedPassword`.

## Relationships

- one `seller` has many `item`
- one `sale` has many `sale_line`
- one `item` may have many historical `sale_line` rows
- one `item` may have at most one active payable `sale_line` where `corrected_at IS NULL AND refunded_at IS NULL`

## Keys And Constraints

## Primary Keys

- `seller.id`
- `item.id`
- `sale.id`
- `sale_line.id`
- `event_state.id`

## Foreign Keys

- `item.seller_id -> seller.id`
- `sale_line.sale_id -> sale.id`
- `sale_line.item_id -> item.id`

## Uniqueness Constraints

- `seller.seller_code` unique
- `item.reference` unique
- `(item.seller_id, item.item_number)` unique
- `sale_line.item_id` unique only for active payable rows where `corrected_at IS NULL AND refunded_at IS NULL`

## Nullability

## `seller`

- `seller_code`: required
- `first_name`: required
- `last_name`: required
- `email`: optional
- `phone`: required column; may be blank while the seller is `deposit_pending`
- `postal_address`: required column; may be blank while the seller is `deposit_pending`
- `postal_code`: required column; may be blank while the seller is `deposit_pending`
- `city`: required column; may be blank while the seller is `deposit_pending`
- `deposit_status`: required
- `notes`: optional

## `item`

- `seller_id`: required
- `reference`: required
- `item_number`: required
- `title`: required
- `seller_price_cents`: required
- `status`: required

## `sale`

- `sold_at`: required
- `subtotal_seller_price_cents`: required
- `checkout_fee_cents`: required
- `total_paid_cents`: required
- `buyer_name`: required for new application-created rows
- `buyer_postal_code`: required for new application-created rows
- `buyer_city`: required for new application-created rows
- `buyer_address`: optional

## `sale_line`

- `sale_id`: required
- `item_id`: required
- `seller_price_at_sale_cents`: required
- `corrected_at`: optional
- `refunded_at`: optional
- `refund_item_price_cents`: optional
- `refund_checkout_fee_cents`: optional
- `refund_total_cents`: optional
- `refund_reason`: optional

## `event_state`

- `sale_phase`: required
- `updated_at`: required

## Enumerations

## Item Status

Allowed values:

- `registered`
- `in_stock`
- `sold`
- `missing`
- `defective`

Business meaning:

- `registered`: entered in the system but not yet accepted through a signed seller deposit; it may remain pending if the seller might still bring the item later
- `in_stock`: accepted through a validated seller deposit, physically present, and eligible for sale or return
- `sold`: the item was sold during the event
- `missing`: the item was not found during post-sale gathering and is therefore payable to the seller
- `defective`: the item cannot be sold, is returned to the seller, and is not payable

V1 does not use a `not_provided` status. If a preregistered item is not physically delivered, it may remain `registered`. It is deleted only when entered by mistake or when the seller record should be cleaned up before deposit validation.

## Seller Deposit Status

Allowed values:

- `deposit_pending`
- `deposit_validated`

Business meaning:

- `deposit_pending`: the seller has not yet signed the printed deposit contract; normal seller and item edits remain possible during `presale`, contact fields may still be incomplete, and items stay `registered`
- `deposit_validated`: the signed paper contract has been received; normal seller and item edits are locked for that seller, and accepted items are `in_stock`

## Sale Phase

Allowed values:

- `presale`
- `sale`
- `post_sale`

## Delete And Update Rules

V1 should be conservative and prefer restriction over cascading deletion.

## Delete Rules

- do not delete a `seller` if items exist for that seller
- allow deleting an `item` only while its status is `registered` or `in_stock`
- do not delete an `item` if it is linked to a sale line
- do not hard-delete a `defective` item in normal operations because it must be returned to the seller
- do not delete a `sale` in normal operations once created
- do not delete `sale_line` in normal operations; sale correction fills `corrected_at`

## Update Rules

- `seller.seller_code` should not change once items exist for the seller
- in `presale`, intake staff may still correct unsold item master data if needed
- in `sale` and `post_sale`, item master data becomes read-only in normal operations
- when item master data is locked, `item.reference`, `item.item_number`, `item.seller_id`, `item.title`, and `item.seller_price_cents` should not change
- when item master data is locked, only workflow-driven status transitions remain allowed
- after `seller.deposit_status = deposit_validated`, normal seller and item edits are locked for that seller even during `presale`
- admin cancellation may set `seller.deposit_status` back to `deposit_pending` during `presale` after shared-password confirmation; it moves only current `in_stock` items back to `registered` and leaves `sold`, `missing`, and `defective` items unchanged
- `sale.subtotal_seller_price_cents`, `sale.checkout_fee_cents`, and `sale.total_paid_cents` should be treated as immutable after creation
- `sale_line` price snapshots are immutable; correction status is represented by `corrected_at`, and defective buyer refunds are represented by the refund fields, which may only be cleared by the explicit admin refund-cancellation workflow

## Business Invariants

These are important invariants even if they are enforced first in application logic rather than as hard database constraints.

- adding an item to a cart does not reserve it in V1
- a sale may only be persisted while `event_state.sale_phase` is `presale` or `sale`
- a sale may only be persisted if every item in the cart is still `in_stock` and its seller has `deposit_status = deposit_validated` at transaction time
- if an item has an active payable sale line where `corrected_at IS NULL AND refunded_at IS NULL`, its status must be `sold`
- one item can only have one active payable sale line
- corrected sale lines do not make an item unavailable for sale
- refunded sale lines do not make an item payable to the seller
- if a sale line has `refunded_at`, then `refund_reason`, `refund_item_price_cents`, `refund_checkout_fee_cents`, and `refund_total_cents` should be filled
- `sale.subtotal_seller_price_cents` must equal the sum of its sale lines' `seller_price_at_sale_cents`
- `sale.checkout_fee_cents` must equal 10 percent of `sale.subtotal_seller_price_cents`, rounded to the nearest cent with midpoint values rounded away from zero
- `sale.total_paid_cents` must equal `sale.subtotal_seller_price_cents + sale.checkout_fee_cents`
- `item.reference` must match the event seller code and seller-local item number used to generate it
- normal `registered -> in_stock` transition is seller-level: validating the signed seller deposit moves that seller's accepted `registered` items to `in_stock`

## Derived Business Meaning

The following outputs are derived from the schema and are not stored as separate V1 tables.

## Payable Items Per Seller

All seller items with:

- `status = sold` and an active payable sale line
- or `status = missing`

Missing items must appear in the payable list with a visible `MANQUANT` mention.

## Returned Items Per Seller

All seller items with:

- `status = in_stock`
- `status = defective`

Defective items must be visibly marked `DÉFECTUEUX`.

## Excluded Items Per Seller

Seller items with:

- `status = registered`

These items are not part of post-sale payable or returned outputs.

## Final Amount To Pay Seller

For V1 seller settlement:

- item subtotal = sum of `seller_price_cents` for all payable items
- expected seller fee = EUR 2 per started block of 15 seller items, including `registered` items
- remaining seller fee = max(expected seller fee - `seller.fee_paid_cents`, 0)
- final payout = max(item subtotal - remaining seller fee, 0)

## Index Plan

Recommended indexes:

- unique index on `seller.seller_code`
- unique index on `item.reference`
- unique index on `(item.seller_id, item.item_number)`
- unique partial index on `sale_line.item_id` where `corrected_at IS NULL AND refunded_at IS NULL`
- index on `item.reference`
- index on `item.title`
- index on `item.seller_id`
- index on `item.status`
- index on `sale.sold_at`
- index on `sale_line.sale_id`

These support:

- fast live-filtered lookup on item reference and title
- seller sheet generation
- post-sale filtering by item status
- event-level sale listing and export

## Reference Generation

The schema assumes references are generated from:

- `seller.seller_code`
- `item.item_number`

Final operator-facing format:

- `SELLERCODENNN`

Example:

- `BK014`

Generation rule:

1. assign the seller code when the seller is created
2. assign the next seller-local item number when a new item is created
3. build and store the final reference

## Engine Notes

### SQLite Fit

- chosen engine for V1
- one SQLite database file per event
- suitable if used only as the central server database
- fine for V1 volume and expected write concurrency
- should run on the server's local disk with `WAL` mode enabled
- keep application-side discipline for business invariants, connection pragmas, and migration management
