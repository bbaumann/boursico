# Physical SQL DDL

This directory contains concrete SQL DDL for the agreed current V1 logical schema.

Files:

- [SQLite schema](./sqlite.sql)

## Purpose

These files are intended to:

- provide the SQLite schema directly
- keep the logical schema and the physical schema aligned

## Important Notes

## 1. The DDL Is Intentionally Conservative

The schemas enforce:

- primary keys
- foreign keys
- uniqueness constraints
- simple status checks
- basic price sanity checks

They intentionally do not enforce every business invariant in the database.

Some rules remain application-level, especially:

- when an item status transitions to `sold`
- sale subtotal matching the sum of sale lines
- checkout fee and total-paid computation
- preventing unsafe edits once an item has been sold

## 2. Reference And Seller-Code Semantics

The database stores the seller code on `seller` and the final item
`reference` on `item`. There is no per-seller item number: `item.reference`
is the only reference-related column, and it is nullable.

An item's `reference` is `NULL` from creation until its seller's deposit
reaches the contract-print step. At that point the application allocates a
single `<LETTER><NNN>` string per item — one letter from the 20-letter safe
alphabet plus a zero-padded 3-digit number within that letter — and persists
it directly; there is no separate letter or number column, and no per-seller
sequence to derive it from. The letter identifies a physical sort bucket for
post-sale recollection, not the seller: `seller_code` and `item.reference`
are independent values after allocation.

The database does not store an `event` table in V1.

Each database file represents one event operationally, and event metadata such as the event name or year code lives in application configuration.

The app is expected to generate and persist the final reference rather than deriving it on every read.

## 3. Autocomplete Indexing Limits

Normal B-tree indexes help well for:

- exact lookup
- prefix lookup
- ordered scans

They do not fully solve arbitrary substring search like `%lego%` on large text fields.

For V1, the current index plan is acceptable if checkout lookup is implemented as:

- prefix search on `reference`
- prefix or lightweight match on `title`

If later you want strong substring/fuzzy search, likely follow-up options are:

- SQLite FTS

## 4. SQLite V1 Specifics

### SQLite

- uses `INTEGER PRIMARY KEY` for row ids
- is the chosen V1 engine
- stores money as integer cents in `INTEGER` columns
- suitable only as the central server database, not as a multi-writer shared-file client architecture

## 5. Transaction Boundary Recommendation

When saving a sale, the application should perform these steps in one transaction:

1. re-check that every cart item is still `in_stock`
2. compute the sale subtotal, checkout fee, and final total
3. create the `sale`
4. create all `sale_line` rows
5. update all corresponding `item.status` rows to `sold`

V1 intentionally does not reserve items at cart time.

If any cart item is no longer `in_stock`, the application must reject the save clearly and let the operator retry after removing the conflicting item.

That transaction boundary matters more than adding clever database-side logic in V1.
