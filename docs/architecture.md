# Architecture

## High-Level Architecture

The recommended V1 architecture is a small custom local-network system:

- one local server machine on the event network
- one central authoritative database
- one browser-based operations UI for intake and sale-day use
- one admin-gated administration page for event phase changes, sale corrections, and exceptional item corrections
- one admin-gated in-app reconciliation dashboard for post-sale processing

This is intentionally not an offline-first distributed architecture.

## Components

### 1. Central Server

Responsibilities:

- serve the web application
- expose the application logic
- connect to the authoritative database

Properties:

- located on the private local network
- internet is optional, not required for the event to run
- single digital point of truth during the event
- runs as one self-hosted executable using Kestrel only in V1

### 2. Central Database

Responsibilities:

- store sellers, items, sales, and statuses
- store the current event sale phase
- preserve corrected sale lines as historical facts
- store the only authoritative event data
- support deterministic post-sale reconciliation

Properties:

- one SQLite database file per event
- no per-station local databases in V1
- no client replay logs in V1

### 3. Operations UI

Responsibilities:

- intake of items before the event
- sale-day lookup and cart creation
- sale saving
- optional receipt printing
- visible current sale phase on every page
- admin-gated administration workflow from `/administration`
- correction of mistaken sale lines and exceptional item states from the unlocked administration workflow

Properties:

- browser-based
- large, simple, high-contrast UI
- French labels and French user-facing URLs
- optimized for elderly volunteers
- very narrow workflow

### 4. Administration

Responsibilities:

- expose event phase controls at `/administration`
- move the event between `presale`, `sale`, and `post_sale`
- require `Admin:SharedPassword` to unlock the page
- list non-`registered` items that can be corrected
- return a mistakenly sold item to `in_stock` while marking the original sale line as corrected
- mark accepted items as `defective`
- refund buyer-returned defective sold items
- cancel a mistaken defective refund and restore the item to `sold`

Properties:

- admin-gated by the shared admin password
- password entry unlocks the page for the current browser session; individual phase and correction actions do not re-prompt
- operational rather than accounting-focused
- phase is persisted in the database and enforced server-side
- corrected sale lines remain in the historical sale record
- defective refunds remain on the historical sale line unless an admin cancels a mistaken refund
- the original sale totals are not recalculated by correction

### 5. Reconciliation Dashboard

Responsibilities:

- expose the reconciliation dashboard at `/rapprochement`
- compute seller-level outputs after sales are over
- print seller sheets from `/rapprochement/feuilles`
- generate sheets only for sellers with at least one `sold`, `missing`, `in_stock`, or `defective` item
- support marking items as `missing`
- support restoring mistakenly marked `missing` items to `in_stock` as reconciliation corrections
- regenerate corrected seller sheets from current database facts

Properties:

- admin-gated by the shared admin password
- uses `Admin:SharedPassword`; production configuration must provide a non-blank value other than `change-me`
- can be opened by administrators before `post_sale` as a read-only view
- seller-sheet printing and correction actions are enabled only in `post_sale`
- can be used by administrators for operations after sales are over

## Data Flow

### Presale Phase

1. Seller list is received outside the system.
2. An organizer manually creates the seller and item records.
3. The system generates references.
4. Items are ready for sale-day lookup.
5. Checkout remains available so operations are not blocked if the phase was not changed before opening.

### Sale Phase

1. Admin moves the event to `sale` from `/administration`.
2. Seller and item master data become read-only in normal operations.
3. `/` opens `/caisse`.
4. Operator types part of a reference.
5. UI shows matching items.
6. Operator selects an item.
7. Item is added to the current cart.
8. Sale is saved.
9. Item status becomes unavailable for further sale.
10. Admin can correct a mistaken sale line from `/administration` if an item was sold by mistake.

### Post-Sale Phase

1. Admin moves the event to `post_sale` from `/administration`.
2. Checkout validation and sale persistence are rejected server-side.
3. `/` opens `/rapprochement`.
4. Reconciliation dashboard reads database facts.
5. Admin prints seller sheets from `/rapprochement/feuilles`.
6. Physical stock is checked against returned item lists.
7. Admin marks missing items where needed or restores mistaken missing marks as corrections.
8. Corrected sheets are regenerated from the dashboard.
9. Final seller payout workflow is executed.

## Architectural Principles

- Keep the system centralized during the event.
- Keep the sale-day UI separate from reconciliation concerns.
- Persist the sale phase centrally and enforce it in services, not only in page navigation.
- Prefer explicit manual fallback over hidden partial offline behavior.
- Optimize for operational clarity rather than technical sophistication.

## Suggested Technical Direction

The chosen technical direction for V1 is:

- an ASP.NET Core web application
- .NET 8 LTS
- Razor Pages for the main UI
- one web project with internal folders rather than a multi-project solution
- vanilla JavaScript only where local interactivity is genuinely needed, mainly checkout
- SQLite on the central server as the V1 database
- `WAL` mode for SQLite concurrency behavior
- in-app reconciliation pages that can evolve independently from checkout

This keeps the architecture small, self-hosted, and operationally simple while preserving a future migration path if the app later outgrows the V1 choices.

## Reasons This Architecture Was Chosen

- The difficult part is reconciliation, not generic POS functionality.
- The sale-day UI must be custom and extremely simple.
- Building local per-station sync would add too much complexity for V1.
- Odoo would solve the wrong 80 percent of the problem for this scope.
