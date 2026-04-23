# Database Choice

## Decision Summary

V1 will use SQLite as the production database engine.

This choice is based on the current project shape:

- one central ASP.NET Core server
- local-network deployment
- fewer than 10 client stations, usually around 5
- short checkout write transactions
- operational simplicity matters more than large-scale database features
- moving the system between machines is easier when the database is a file rather than a separately managed service

PostgreSQL remains a plausible future migration target, but it is not the default for V1.

## Why SQLite Was Chosen

### Main Advantages For This Project

- no separate database installation or service management
- much simpler machine-to-machine transfer
- easy backup story for a small event tool
- sufficient read/write capacity for the expected number of sale stations
- strong fit with one web application talking to one local database file on the server

### Why PostgreSQL Was Not Chosen For V1

PostgreSQL would provide:

- stronger write concurrency
- richer administrative tooling
- more long-term headroom

For this project, those benefits do not currently outweigh:

- extra installation and operational complexity on Windows
- more moving parts during pre-sale to sale-day server transfer
- more backup, restore, and service-management work for a once-a-year tool

## Operational Rules

These rules are part of the database decision, not optional implementation details.

### 1. SQLite Runs Only On The Server

- the SQLite database file lives on the local disk of the central server
- clients never open the database directly
- clients talk only to the ASP.NET Core application
- the database file must not be placed on a shared network drive

### 2. WAL Mode Is Required

SQLite must run in `WAL` mode.

Why:

- reduces reader/writer interference
- fits the "many reads, short writes" pattern of this application
- helps the checkout flow remain responsive while sale writes are happening

### 3. Foreign Keys Must Be Enforced

SQLite foreign key enforcement must be enabled on connections.

Why:

- the data model depends on seller/item/sale/sale-line integrity
- foreign keys should not be treated as documentation only

### 4. Money Is Stored As Integer Cents

All monetary values are stored as integer cents in the database.

Examples:

- `250` means `2.50 EUR`
- `180` means `1.80 EUR`

Why:

- avoids floating-point approximation issues
- makes totals, comparisons, and invariants exact
- is simple to format back into human-readable prices in the app

### 5. Timestamps Must Use UTC

Application timestamps should be stored in UTC using one consistent format.

For SQLite, the preferred storage format is UTC ISO 8601 text.

### 6. Write Transactions Must Stay Short

Checkout writes should stay tight and transactional:

1. re-check item availability
2. insert the sale
3. insert sale lines
4. update sold item statuses
5. commit

The app should also use a reasonable `busy_timeout` and treat brief lock contention as a normal transient condition.

### 7. Backup And Transfer Need A Procedure

Because SQLite in `WAL` mode may use multiple files, database copy or machine transfer must be done with a clean procedure.

At minimum:

- stop the app cleanly before copying the database
- copy the SQLite database files as a consistent set
- do not rely on ad hoc live file copies during active use

## Revisit Triggers

The project should reconsider PostgreSQL if one or more of these become true:

- checkout stations increase materially beyond the current expectation
- real lock contention appears during realistic concurrency tests
- the app grows into a broader multi-user admin system
- operational needs start requiring richer database tooling than SQLite provides comfortably

## Final Position

For V1, SQLite is the right trade-off:

- simpler to deploy
- simpler to move between machines
- sufficient for the expected concurrency
- compatible with the narrow, centralized architecture already chosen for the app
