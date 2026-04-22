# Database Schema

## Purpose

This document defines the V1 logical database schema for the association toy sale tool.

It is intentionally engine-agnostic:

- no SQL dialect is assumed yet
- the model should map cleanly to SQLite on a central server or to PostgreSQL later

The schema is designed for a minimal normalized relational model.

Concrete DDL drafts are available in:

- [SQLite DDL](./sql/sqlite.sql)
- [PostgreSQL DDL](./sql/postgresql.sql)
- [DDL Notes](./sql/README.md)

## Design Principles

- One physical toy equals one item record.
- One completed checkout equals one sale.
- One sold toy appears in exactly one sale line.
- Seller sheets are derived from current data, not stored as their own primary business objects in V1.
- The schema supports multiple events logically, even if V1 may run one event per database operationally.

## Tables

## `event`

Represents one sale event.

### Columns

- `id`
- `code`
- `name`

### Notes

- `code` is a short identifier such as `2026`.
- No event dates or status fields are included in V1.

## `seller`

Represents one seller for one event.

### Columns

- `id`
- `event_id`
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
- `event_id`
- `seller_id`
- `reference`
- `item_number`
- `title`
- `seller_price`
- `sale_price`
- `status`

### Notes

- `reference` is the human-facing code, e.g. `BK-014`.
- `item_number` is the seller-local internal sequence used to build the reference.
- `title` is the item label shown in search results, carts, and sheets.
- `status` uses the V1 status model: `registered`, `in_stock`, `not_provided`, `sold`, `missing`.

## `sale`

Represents one completed checkout.

### Columns

- `id`
- `event_id`
- `sold_at`
- `total_amount`
- `receipt_requested`
- `receipt_name`
- `receipt_organization`

### Notes

- There is no `is_paid` field in V1; persisting the sale means the checkout is complete.
- Receipt fields are optional and only used in the rare reimbursement case.

## `sale_line`

Represents one sold item inside one sale.

### Columns

- `id`
- `sale_id`
- `item_id`
- `seller_price_at_sale`
- `sale_price_at_sale`

### Notes

- Price snapshots are stored on the sale line to protect reconciliation if item data is later edited by mistake.
- An item can appear in at most one sale line.

## Relationships

- one `event` has many `seller`
- one `event` has many `item`
- one `event` has many `sale`
- one `seller` has many `item`
- one `sale` has many `sale_line`
- one `item` has at most one `sale_line`

## Keys And Constraints

## Primary Keys

- `event.id`
- `seller.id`
- `item.id`
- `sale.id`
- `sale_line.id`

## Foreign Keys

- `seller.event_id -> event.id`
- `item.event_id -> event.id`
- `item.seller_id -> seller.id`
- `sale.event_id -> event.id`
- `sale_line.sale_id -> sale.id`
- `sale_line.item_id -> item.id`

## Uniqueness Constraints

- `event.code` unique
- `(seller.event_id, seller.seller_code)` unique
- `(item.event_id, item.reference)` unique
- `(item.seller_id, item.item_number)` unique
- `sale_line.item_id` unique

## Nullability

## `event`

- `code`: required
- `name`: required

## `seller`

- `event_id`: required
- `seller_code`: required
- `first_name`: required
- `last_name`: required
- `email`: optional
- `phone`: optional
- `notes`: optional

## `item`

- `event_id`: required
- `seller_id`: required
- `reference`: required
- `item_number`: required
- `title`: required
- `seller_price`: required
- `sale_price`: required
- `status`: required

## `sale`

- `event_id`: required
- `sold_at`: required
- `total_amount`: required
- `receipt_requested`: required, default `false`
- `receipt_name`: optional
- `receipt_organization`: optional

## `sale_line`

- `sale_id`: required
- `item_id`: required
- `seller_price_at_sale`: required
- `sale_price_at_sale`: required

## Enumerations

## Item Status

Allowed values:

- `registered`
- `in_stock`
- `not_provided`
- `sold`
- `missing`

Business meaning:

- `registered`: preregistered in the system but not yet physically delivered
- `in_stock`: physically present and eligible for sale or return
- `not_provided`: preregistered but never physically delivered
- `sold`: the item was sold during the event
- `missing`: the item was not found during post-sale gathering and is therefore payable to the seller

## Delete And Update Rules

V1 should be conservative and prefer restriction over cascading deletion.

## Delete Rules

- do not delete an `event` once business data exists for it
- do not delete a `seller` if items exist for that seller
- do not delete an `item` if it is linked to a sale line
- do not delete a `sale` in normal operations once created
- do not delete `sale_line` in normal operations

## Update Rules

- `seller.seller_code` should not change once items exist for the seller
- before sale start, intake staff may still correct item master data if needed
- once sale-day checkout opens, item master data becomes read-only in normal operations
- after sale start, `item.reference`, `item.item_number`, `item.seller_id`, `item.title`, `item.seller_price`, and `item.sale_price` should not change
- after sale start, only workflow-driven status transitions remain allowed in V1
- `sale.total_amount` should be treated as immutable after creation
- `sale_line` snapshots are immutable

## Business Invariants

These are important invariants even if they are enforced first in application logic rather than as hard database constraints.

- adding an item to a cart does not reserve it in V1
- a sale may only be persisted if every item in the cart is still `in_stock` at transaction time
- if an item appears in `sale_line`, its status must be `sold`
- one item can only be sold once
- `sale.total_amount` must equal the sum of its sale lines' `sale_price_at_sale`
- `item.reference` must match the event seller code and seller-local item number used to generate it

## Derived Business Meaning

The following outputs are derived from the schema and are not stored as separate V1 tables.

## Payable Items Per Seller

All seller items with:

- `status = sold`
- or `status = missing`

Missing items must appear in the payable list with a visible `MISSING` mention.

## Returned Items Per Seller

All seller items with:

- `status = in_stock`

## Excluded Items Per Seller

Seller items with:

- `status = registered`
- `status = not_provided`

These items are not part of post-sale payable or returned outputs.

## Final Amount To Pay Seller

For V1:

- sum of `seller_price` for all payable items

The EUR 2 per 15 items seller fee stays outside the system in V1.

## Index Plan

Recommended indexes:

- unique index on `event.code`
- unique index on `(seller.event_id, seller.seller_code)`
- unique index on `(item.event_id, item.reference)`
- unique index on `(item.seller_id, item.item_number)`
- unique index on `sale_line.item_id`
- index on `item.reference`
- index on `item.title`
- index on `item.seller_id`
- index on `item.status`
- index on `(sale.event_id, sale.sold_at)`
- index on `sale_line.sale_id`

These support:

- fast autocomplete on item reference and title
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

This logical schema is deliberately portable.

### SQLite Fit

- suitable if used only as the central server database
- fine for V1 volume and constraints
- keep application-side discipline for business invariants and migration management

### PostgreSQL Fit

- cleaner long-term path for stronger constraints, richer indexing, and future growth
- likely a better default once deployment complexity is acceptable

The schema itself should not block either choice.
