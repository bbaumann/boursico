# Physical SQL DDL

This directory contains concrete SQL DDL drafts for the agreed V1 logical schema.

Files:

- [SQLite schema](./sqlite.sql)
- [PostgreSQL schema](./postgresql.sql)

## Purpose

These files are intended to:

- make engine tradeoffs explicit
- let implementation start quickly on either SQLite or PostgreSQL
- keep the logical schema and the physical schema aligned

## Important Notes

## 1. The DDL Is Intentionally Conservative

The schemas enforce:

- primary keys
- foreign keys
- uniqueness constraints
- simple status and boolean checks
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
- uses `NUMERIC` affinity for prices
- uses integer `0/1` plus `CHECK` for booleans
- suitable only as the central server database, not as a multi-writer shared-file client architecture

### PostgreSQL

- uses `BIGINT GENERATED ALWAYS AS IDENTITY`
- uses `BOOLEAN`
- uses `NUMERIC(10,2)` for prices
- gives a cleaner path for future stronger constraints and richer indexing

## 5. Transaction Boundary Recommendation

When saving a sale, the application should perform these steps in one transaction:

1. create the `sale`
2. create all `sale_line` rows
3. update all corresponding `item.status` rows to `sold`

That transaction boundary matters more than adding clever database-side logic in V1.
