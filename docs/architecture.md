# Architecture

## High-Level Architecture

The recommended V1 architecture is a small custom local-network system:

- one local server machine on the event network
- one central authoritative database
- one browser-based operations UI for intake and sale-day use
- one separate reconciliation tool or script for post-sale processing

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

### 2. Central Database

Responsibilities:

- store sellers, items, sales, and statuses
- store the only authoritative event data
- support deterministic post-sale reconciliation

Properties:

- only one database for the event
- no per-station local databases in V1
- no client replay logs in V1

### 3. Operations UI

Responsibilities:

- intake of items before the event
- sale-day lookup and cart creation
- sale saving
- optional receipt printing

Properties:

- browser-based
- large, simple, high-contrast UI
- optimized for elderly volunteers
- very narrow workflow

### 4. Reconciliation Tool

Responsibilities:

- compute seller-level outputs after sales are over
- generate printable seller sheets
- support marking items as missing
- regenerate corrected seller sheets

Properties:

- can be a separate UI, console application, or script in V1
- does not need to run during sales
- can be used by more technical staff first

## Data Flow

### Intake Phase

1. Seller list is received outside the system.
2. An organizer manually creates the seller and item records.
3. The system generates references.
4. Items are ready for sale-day lookup.

### Sale-Day Phase

1. Operator types part of a reference.
2. UI shows matching items.
3. Operator selects an item.
4. Item is added to the current cart.
5. Sale is saved.
6. Item status becomes unavailable for further sale.

### Post-Sale Phase

1. Reconciliation tool reads database facts.
2. Seller sheets are generated.
3. Physical stock is checked against returned item lists.
4. Admin marks missing items where needed.
5. Sheets are regenerated.
6. Final seller payout workflow is executed.

## Architectural Principles

- Keep the system centralized during the event.
- Keep the sale-day UI separate from reconciliation concerns.
- Prefer explicit manual fallback over hidden partial offline behavior.
- Optimize for operational clarity rather than technical sophistication.

## Suggested Technical Direction

No specific framework is locked yet, but the architecture assumes:

- a simple web app rather than a desktop client
- a server-rendered UI or light frontend rather than a heavy SPA by default
- a database-backed backend with clear domain entities
- a separate reconciliation implementation that can evolve independently

The exact framework and database are still open implementation choices.

## Reasons This Architecture Was Chosen

- The difficult part is reconciliation, not generic POS functionality.
- The sale-day UI must be custom and extremely simple.
- Building local per-station sync would add too much complexity for V1.
- Odoo would solve the wrong 80 percent of the problem for this scope.
