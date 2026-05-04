# Database Schema

## Purpose

This document defines the V1 logical database schema for the association toy sale tool.

V1 will run on SQLite on the central server.

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
- One active sold toy appears in exactly one uncorrected sale line.
- Corrected sale lines remain as historical correction evidence.
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
- `notes`

### Notes

- `seller_code` is the two-letter safe code used in item references.
- No stored `display_name` is required; it can be derived when needed.

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

- `reference` is the human-facing code, e.g. `BK-014`.
- `item_number` is the seller-local internal sequence used to build the reference.
- `title` is the item label shown in search results, carts, and sheets.
- `seller_price_cents` is both the seller payout price and the printed per-item customer price in V1.
- money values are stored as integer cents, e.g. `250` for `2.50 EUR`.
- `status` uses the V1 status model: `registered`, `in_stock`, `sold`, `missing`.

## `sale`

Represents one completed checkout.

### Columns

- `id`
- `sold_at`
- `subtotal_seller_price_cents`
- `checkout_fee_cents`
- `total_paid_cents`
- `receipt_details`

### Notes

- There is no `is_paid` field in V1; persisting the sale means the checkout is complete.
- `sold_at` should be stored in UTC.
- `subtotal_seller_price_cents`, `checkout_fee_cents`, and `total_paid_cents` are stored as integer cents.
- `receipt_details` is an optional free-text field used only in the rare reimbursement case.
- If `receipt_details` is null, no receipt was requested in V1.

## `sale_line`

Represents one sold item inside one sale.

### Columns

- `id`
- `sale_id`
- `item_id`
- `seller_price_at_sale_cents`
- `corrected_at`

### Notes

- `seller_price_at_sale_cents` is the per-item snapshot stored to protect reconciliation if item data is later edited by mistake.
- In V1, this snapshot is also the printed item price seen by the customer.
- Snapshot prices are stored as integer cents.
- `corrected_at` is null for an active sale line.
- If `corrected_at` is filled, the line was corrected by an admin and the linked item can return to `in_stock`.
- Original sale totals are not recalculated when a sale line is corrected.

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
- phase changes are made through `/vente/statut` and require `Admin:SharedPassword`.

## Relationships

- one `seller` has many `item`
- one `sale` has many `sale_line`
- one `item` may have many historical `sale_line` rows
- one `item` may have at most one active `sale_line` where `corrected_at IS NULL`

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
- `sale_line.item_id` unique only for active rows where `corrected_at IS NULL`

## Nullability

## `seller`

- `seller_code`: required
- `first_name`: required
- `last_name`: required
- `email`: optional
- `phone`: optional
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
- `receipt_details`: optional

## `sale_line`

- `sale_id`: required
- `item_id`: required
- `seller_price_at_sale_cents`: required
- `corrected_at`: optional

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

Business meaning:

- `registered`: preregistered in the system but not yet physically delivered
- `in_stock`: physically present and eligible for sale or return
- `sold`: the item was sold during the event
- `missing`: the item was not found during post-sale gathering and is therefore payable to the seller

V1 does not use a `not_provided` status. If a preregistered item is not physically delivered, the item is deleted while still `registered`.

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
- do not delete a `sale` in normal operations once created
- do not delete `sale_line` in normal operations; sale correction fills `corrected_at`

## Update Rules

- `seller.seller_code` should not change once items exist for the seller
- in `presale`, intake staff may still correct unsold item master data if needed
- in `sale` and `post_sale`, item master data becomes read-only in normal operations
- when item master data is locked, `item.reference`, `item.item_number`, `item.seller_id`, `item.title`, and `item.seller_price_cents` should not change
- when item master data is locked, only workflow-driven status transitions remain allowed in V1
- `sale.subtotal_seller_price_cents`, `sale.checkout_fee_cents`, and `sale.total_paid_cents` should be treated as immutable after creation
- `sale_line` price snapshots are immutable; correction status is represented only by `corrected_at`

## Business Invariants

These are important invariants even if they are enforced first in application logic rather than as hard database constraints.

- adding an item to a cart does not reserve it in V1
- a sale may only be persisted while `event_state.sale_phase` is `presale` or `sale`
- a sale may only be persisted if every item in the cart is still `in_stock` at transaction time
- if an item has an active sale line where `corrected_at IS NULL`, its status must be `sold`
- one item can only have one active sale line
- corrected sale lines do not make an item unavailable for sale
- `sale.subtotal_seller_price_cents` must equal the sum of its sale lines' `seller_price_at_sale_cents`
- `sale.checkout_fee_cents` must equal 10 percent of `sale.subtotal_seller_price_cents`, rounded to the nearest cent with midpoint values rounded away from zero
- `sale.total_paid_cents` must equal `sale.subtotal_seller_price_cents + sale.checkout_fee_cents`
- `item.reference` must match the event seller code and seller-local item number used to generate it

## Derived Business Meaning

The following outputs are derived from the schema and are not stored as separate V1 tables.

## Payable Items Per Seller

All seller items with:

- `status = sold`
- or `status = missing`

Missing items must appear in the payable list with a visible `MANQUANT` mention.

## Returned Items Per Seller

All seller items with:

- `status = in_stock`

## Excluded Items Per Seller

Seller items with:

- `status = registered`

These items are not part of post-sale payable or returned outputs.

## Final Amount To Pay Seller

For V1:

- sum of `seller_price_cents` for all payable items

The EUR 2 per 15 items seller fee stays outside the system in V1.

## Index Plan

Recommended indexes:

- unique index on `seller.seller_code`
- unique index on `item.reference`
- unique index on `(item.seller_id, item.item_number)`
- unique partial index on `sale_line.item_id` where `corrected_at IS NULL`
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

- `SELLERCODE-NNN`

Example:

- `BK-014`

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
