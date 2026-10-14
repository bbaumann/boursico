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
- Physical caisses are configured centrally and referenced by sales and refund payments.
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
- `identity_document_type`
- `identity_document_number`
- `identity_document_issued_on`
- `fee_paid_cents`
- `deposit_status`
- `deposit_started_at`
- `notes`
- `import_notes`

### Notes

- `seller_code` is the two-letter safe code that identifies the seller. It does not influence item references.
- No stored `display_name` is required; it can be derived when needed.
- `fee_paid_cents` stores the manually collected seller registration fee amount and defaults to `0`.
- The expected seller fee is derived from the seller's item count and is not stored. `registered` items count because they remain seller items in the system.
- `deposit_status` stores the operational seller deposit gate and defaults to `deposit_pending`.
- `deposit_started_at` is null until the operator clicks `Commencer le dépôt` on the seller page; once set, saving new item rows allocates their references in the same save. Canceling a validated deposit keeps it.
- `import_notes` stores durable warnings from the external form import, such as ignored item lines or a submitted city that could not be reconciled.
- Phone, postal address, postal code, city, and identity document fields may be blank while `deposit_status = deposit_pending`; the application requires them before deposit validation.
- The signed paper deposit contract is the legal proof of deposit and price agreement; V1 does not store a contract number, signed timestamp, or contract snapshot.

## `item`

Represents one physical toy.

### Columns

- `id`
- `seller_id`
- `reference`
- `title`
- `seller_price_cents`
- `status`

### Notes

- `reference` is the human-facing code, e.g. `B157`: one letter from the safe
  alphabet plus a zero-padded 3-digit number. It is nullable and stays `NULL`
  until the seller's deposit is started at a deposit desk (or, for a row added
  after that, until the row is saved), and is unique whenever it is not null.
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
- `cash_register_id`
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
- `cash_register_id` points to the configured physical caisse used for the sale.
- `cash_register_id` is required for new checkout sales.
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

## `cash_register`

Represents one configured physical caisse.

### Columns

- `id`
- `name`
- `initial_cash_cents`
- `is_active`
- `display_order`

### Notes

- `name` is the operator-facing caisse name shown in `/caisse`, `/administration`, and closing reports.
- `initial_cash_cents` stores the `fond de caisse` physically placed in the caisse before sales start.
- `is_active` hides obsolete caisses from new sale and refund selection without deleting historical references.
- `display_order` controls the order in dropdowns and closing reports.

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
- `refund_cash_register_id`

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
- `refund_cash_register_id` stores the configured physical caisse that paid the refund.
- Original sale totals are not recalculated when a sale line is corrected.
- Original sale totals are not recalculated when a sale line is refunded.
- If an admin cancels a defective refund that was entered by mistake, the refund fields are cleared and the original sale totals still remain unchanged.

## `event_state`

Represents mutable event-level operational state.

V1 uses a single row with `id = 1`.

### Columns

- `id`
- `sale_phase`
- `labels_per_letter`
- `updated_at`

### Notes

- `sale_phase` values are `presale`, `sale`, and `post_sale`.
- a new database starts in `presale`.
- `labels_per_letter` is the number of pre-printed labels per reference letter, between 1 and 999, default 455, edited on `/administration`. A letter is exhausted when its `last_number` reaches it; exhaustion is computed, never stored.
- phase changes are made through `/administration` and require `Admin:SharedPassword`.

## `deposit_desk`

Represents one computer at one physical deposit queue.

### Columns

- `id`
- `name`: required, trimmed, non-blank
- `token`: required, unique, random; stored in the `poste-depot` cookie
- `opened_at`: UTC
- `closed_at`: null while the desk is open
- `last_activity_at`: UTC, updated by each successful allocation

## `reference_letter`

One row per letter of the 20-letter safe alphabet, seeded by the migration.

### Columns

- `letter`: primary key, one character
- `last_number`: required, default `0`, check `0..999`; only ever increases
- `held_by_desk_id`: nullable foreign key to `deposit_desk`; null when the letter is free

### Notes

- at most one open desk holds a letter. Taking a free letter is a conditional update on `held_by_desk_id IS NULL`, so two desks racing for the same letter cannot both succeed.
- `held_by_desk_id` is also a concurrency token: a stale write fails and the allocation is retried by the operator.
- deleting an item never decreases `last_number`, so a number whose label was already peeled is never handed out again.

## Relationships

- one `seller` has many `item`
- one `deposit_desk` holds zero or more `reference_letter` rows (normally two)
- one `cash_register` has many `sale`
- one `cash_register` may have many refunding `sale_line` rows
- one `sale` has many `sale_line`
- one `item` may have many historical `sale_line` rows
- one `item` may have at most one active payable `sale_line` where `corrected_at IS NULL AND refunded_at IS NULL`

## Keys And Constraints

## Primary Keys

- `seller.id`
- `item.id`
- `sale.id`
- `cash_register.id`
- `sale_line.id`
- `event_state.id`
- `deposit_desk.id`
- `reference_letter.letter`

## Foreign Keys

- `item.seller_id -> seller.id`
- `reference_letter.held_by_desk_id -> deposit_desk.id`
- `sale.cash_register_id -> cash_register.id`
- `sale_line.sale_id -> sale.id`
- `sale_line.item_id -> item.id`
- `sale_line.refund_cash_register_id -> cash_register.id`

## Uniqueness Constraints

- `seller.seller_code` unique
- `item.reference` unique whenever not null
- `deposit_desk.token` unique
- `cash_register.name` unique
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
- `identity_document_type`: optional column; required by the application before deposit validation
- `identity_document_number`: optional column; required by the application before deposit validation
- `identity_document_issued_on`: optional date-only column; required by the application before deposit validation
- `deposit_status`: required
- `deposit_started_at`: optional
- `notes`: optional
- `import_notes`: optional

## `item`

- `seller_id`: required
- `reference`: optional; null until the seller's deposit is started at a
  deposit desk, then set once and kept
- `title`: required
- `seller_price_cents`: required
- `status`: required

## `sale`

- `sold_at`: required
- `cash_register_id`: required
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
- `refund_cash_register_id`: optional

## `event_state`

- `sale_phase`: required
- `labels_per_letter`: required
- `updated_at`: required

## `cash_register`

- `name`: required
- `initial_cash_cents`: required
- `is_active`: required
- `display_order`: required

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

- `deposit_pending`: the seller has not yet signed the printed deposit contract; normal seller and item edits remain possible during `presale`, contact and identity document fields may still be incomplete, and items stay `registered`
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
- when item master data is locked, `item.reference`, `item.seller_id`, `item.title`, and `item.seller_price_cents` should not change
- when item master data is locked, only workflow-driven status transitions remain allowed
- after `seller.deposit_status = deposit_validated`, normal seller and item edits are locked for that seller even during `presale`
- admin cancellation may set `seller.deposit_status` back to `deposit_pending` during `presale` after shared-password confirmation; it moves only current `in_stock` items back to `registered` and leaves `sold`, `missing`, and `defective` items unchanged
- `sale.cash_register_id`, `sale.subtotal_seller_price_cents`, `sale.checkout_fee_cents`, and `sale.total_paid_cents` should be treated as immutable after creation
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
- if a sale line has `refunded_at`, then `refund_reason`, `refund_item_price_cents`, `refund_checkout_fee_cents`, `refund_total_cents`, and `refund_cash_register_id` should be filled
- `sale.subtotal_seller_price_cents` must equal the sum of its sale lines' `seller_price_at_sale_cents`
- `sale.checkout_fee_cents` must equal 10 percent of `sale.subtotal_seller_price_cents`, rounded to the nearest cent with midpoint values rounded away from zero
- `sale.total_paid_cents` must equal `sale.subtotal_seller_price_cents + sale.checkout_fee_cents`
- `item.reference`, once allocated, encodes only a letter-bucket and a
  zero-padded sequence number within it; it is never regenerated or reparsed
  to recover the seller
- `reference_letter.last_number` never decreases and never exceeds `event_state.labels_per_letter` through allocation; `labels_per_letter` cannot be set below the highest `last_number`
- at most one open `deposit_desk` holds a given letter
- normal `registered -> in_stock` transition is seller-level: validating the signed seller deposit moves that seller's accepted `registered` items to `in_stock`
- seller deposit validation requires complete seller contact fields, complete identity document fields, and at least one complete item row
- an item with `status = registered` must never be moved to `in_stock` while it still has a null `reference`; `IntakeService.ValidateSellerDepositAsync` enforces this server-side, independently of the sequencing between `Commencer le dépôt` and deposit validation

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
- sale fee = 15 percent of item subtotal, rounded to the nearest cent with midpoint values rounded away from zero
- expected seller fee = EUR 2 per started block of 15 seller items, including `registered` items
- remaining seller fee = max(expected seller fee - `seller.fee_paid_cents`, 0)
- final payout = max(item subtotal - sale fee - remaining seller fee, 0)

## Index Plan

Recommended indexes:

- unique index on `seller.seller_code`
- unique index on `item.reference` (SQLite treats `NULL` values as distinct, so any number of `registered` items may share a `NULL` reference)
- unique index on `cash_register.name`
- unique partial index on `sale_line.item_id` where `corrected_at IS NULL AND refunded_at IS NULL`
- index on `item.reference`
- index on `item.title`
- index on `item.seller_id`
- index on `item.status`
- index on `sale.sold_at`
- index on `sale.cash_register_id`
- index on `sale_line.sale_id`
- index on `sale_line.refund_cash_register_id`

These support:

- fast live-filtered lookup on item reference and title
- seller sheet generation
- post-sale filtering by item status
- event-level sale listing and export
- financial closing totals by configured caisse, including initial cash amounts
- sorting a seller's items by `reference`, which the `item.reference` index serves; allocation itself reads only the 20 `reference_letter` rows

## Reference Generation

References are no longer derived from the seller code. Each open deposit desk
owns a few letters (`Labels:LettersPerDesk`, default 2), and a reference is
allocated, letter and number together as one string, from the desk's letters
when rows are saved at that desk. See `docs/domain-model-and-references.md`
and `docs/superpowers/specs/2026-10-13-deposit-desk-letters-design.md`.

Final operator-facing format:

- `<LETTER><NNN>`

Example:

- `B157`

`LETTER` is one character of the 20-letter safe alphabet already used for
seller codes (`B C D E G H J K L M N P R S T U W X Y Z`). `NNN` is the
number within that letter, zero-padded to exactly 3 digits; the padding is
load-bearing because `ORDER BY reference` must sort in physical label order.

Allocation rule, run inside the transaction that saves the rows, at the open
desk resolved from the `poste-depot` cookie, only during `presale`:

1. `Commencer le dépôt` allocates a run to every row of the seller that has no
   reference, in `item.id` order, and sets `seller.deposit_started_at`. Once
   the deposit is started, saving a new row allocates its reference in the
   same save. Saving rows on a non-started deposit allocates nothing.
2. the next number in a letter is `reference_letter.last_number + 1`; the
   counter is incremented, never decremented
3. choose the letter(s): the seller's most recent letter if the desk holds it
   and it has room for the whole run; otherwise the usable held letter with the
   most room if it can take the whole run; otherwise split across the seller's
   letter, the other held letters (most room first) and free letters taken on
   the spot (least used first); if that cannot provide the run, nothing is
   written
4. write `reference = letter + number.ToString("000")`; the reference string
   is the single source of truth, with no separate letter or number column to
   fall out of step with it
5. release held letters that are now exhausted and top the desk back up to
   `Labels:LettersPerDesk` usable letters

A letter is exhausted when `last_number >= event_state.labels_per_letter`
(default 455, edited on `/administration`). The application never reasons
about label sheets, only about that number. Canceling a validated deposit keeps
the references and the started marker.

## Engine Notes

### SQLite Fit

- chosen engine for V1
- one SQLite database file per event
- suitable if used only as the central server database
- fine for V1 volume and expected write concurrency
- should run on the server's local disk with `WAL` mode enabled
- keep application-side discipline for business invariants, connection pragmas, and migration management
