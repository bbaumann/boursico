# Physical SQL DDL

This directory contains concrete SQL DDL for the agreed V1 logical schema.

Files:

- [SQLite schema](./sqlite.sql)
- [PostgreSQL reference schema](./postgresql.sql)

## Purpose

These files are intended to:

- provide the V1 SQLite schema directly
- keep a PostgreSQL reference for a possible later migration path
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

Some rules remain application-level in V1, especially:

- when an item status transitions to `sold`
- sale total matching the sum of sale lines
- preventing unsafe edits once an item has been sold

## 2. Reference And Seller-Code Semantics

The database stores both:

- the seller code
- the seller-local item number
- the final reference

The database does not store an `event` table in V1.

Each database file represents one event operationally, and event metadata such as the event name or year code lives in application configuration.

The app is expected to generate and persist the final reference rather than deriving it on every read.

## 3. Autocomplete Indexing Limits

Normal B-tree indexes help well for:

- exact lookup
- prefix lookup
- ordered scans

They do not fully solve arbitrary substring search like `%lego%` on large text fields.

For V1, the current index plan is acceptable if autocomplete is implemented as:

- prefix search on `reference`
- prefix or lightweight match on `title`

If later you want strong substring/fuzzy search, likely follow-up options are:

- SQLite FTS
- PostgreSQL trigram indexes (`pg_trgm`)

## 4. SQLite Vs PostgreSQL Differences

### SQLite

- uses `INTEGER PRIMARY KEY` for row ids
- is the chosen V1 engine
- stores money as integer cents in `INTEGER` columns
- suitable only as the central server database, not as a multi-writer shared-file client architecture

### PostgreSQL

- uses `BIGINT GENERATED ALWAYS AS IDENTITY`
- is kept as a migration reference, not as the chosen V1 default
- can remain a future path for stronger concurrency and richer database operations

## 5. Transaction Boundary Recommendation

When saving a sale, the application should perform these steps in one transaction:

1. re-check that every cart item is still `in_stock`
2. create the `sale`
3. create all `sale_line` rows
4. update all corresponding `item.status` rows to `sold`

V1 intentionally does not reserve items at cart time.

If any cart item is no longer `in_stock`, the application must reject the save clearly and let the operator retry after removing the conflicting item.

That transaction boundary matters more than adding clever database-side logic in V1.
