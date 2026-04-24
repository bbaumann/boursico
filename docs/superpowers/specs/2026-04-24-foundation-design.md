# Foundation Implementation Design

## Scope

This design covers the first implementation slice for the association sale tool:

- scaffold the ASP.NET Core web application
- bind the V1 configuration contract from `appsettings.json`
- establish the SQLite and EF Core foundation
- encode the core domain rules needed by later workflows
- prepare the internal structure for intake, checkout, and reconciliation

This slice does not implement the full intake, checkout, or reconciliation workflows yet. It creates the stable base those features will build on.

## Goals

- Match the canonical documentation in `docs/`
- Keep the solution as one deployable ASP.NET Core application
- Keep page handlers thin and business rules explicit
- Make the database schema, configuration, and domain rules concrete early
- Leave clear seams for the first real feature slices

## Non-Goals

- Full end-to-end intake workflow
- Full interactive checkout experience
- Seller-sheet generation UI
- Receipt printing flow
- Full authentication or user management

## Architecture

The application will be one ASP.NET Core Razor Pages web app on .NET 8. The app will be self-hosted with Kestrel and use SQLite as the only database engine in V1.

The project will stay as a single web project with internal folders instead of a multi-project solution. The folder boundaries will be:

- `Pages` for Razor Pages and thin page models
- `Domain` for business concepts and rules
- `Data` for EF Core entities and the DbContext
- `Services` for application workflows
- `Infrastructure` for configuration, persistence setup, and framework-facing support code

This keeps the structure simple while still separating business rules from UI and EF concerns.

## Domain Boundaries

The `Domain` layer will hold the stable rules defined by the docs:

- item status values and allowed transitions
- seller code generation using the safe two-letter alphabet
- per-seller item numbering
- item reference formatting as `SELLERCODE-NNN`
- price calculations where they are part of the documented business rule

These rules will be implemented as explicit code rather than being spread across Razor handlers or EF configuration. That keeps the important invariants readable and testable.

## Data Model

The EF Core model will mirror the documented schema:

- `Seller`
- `Item`
- `Sale`
- `SaleLine`

Key constraints from the docs will be represented in the model and initial migration:

- unique `seller_code`
- unique `reference`
- unique `(seller_id, item_number)`
- unique `sale_line.item_id`
- restricted foreign keys
- item status constraint
- non-negative money values

Money values will be stored as integer cents. Sale timestamps will be stored in UTC. The app will treat one SQLite database file as one event.

## Configuration

The app will bind these V1 configuration sections from `appsettings.json`:

- `Database`
- `Event`
- `Admin`
- `Printing`

The concrete V1 contract will be:

- `Database`: SQLite file path only
- `Event`: event name only
- `Admin`: shared admin password only
- `Printing`: receipt header text only

Runtime database behaviors such as foreign keys, WAL mode expectations, and retry or timeout behavior will stay in application code, not as extra config switches.

## Application Services

The foundation slice will define the workflow seams now, even if most handlers remain placeholders:

- `IntakeService` for seller and item creation plus preregistration confirmation later
- `CheckoutService` for catalog loading, validation, and sale persistence later
- `ReconciliationService` for seller-sheet queries and `missing` transitions later

In the first slice, these services may start small or partially stubbed, but their responsibilities will already match the documented workflow split.

## UI Shape

Most of the app will be normal server-rendered Razor Pages. The checkout page is the only planned interactive frontend island.

The foundation slice will create:

- a minimal home page
- basic route structure for future feature areas
- shared layout and CSS baseline

Checkout-specific JavaScript will not be fully built in this slice, but the future boundary is fixed now:

- Razor owns page structure and shells
- JavaScript owns in-memory checkout interaction
- the server remains authoritative for validation and persistence

## API Shape

The app will stay page-oriented, with a narrow resource-style API surface only where interactive behavior requires it.

Planned endpoint directions are:

- HTML pages under normal Razor routes such as `/checkout`, `/intake/...`, and `/reconciliation/...`
- interactive endpoints under `/api/...` for checkout catalog load, pre-finalize validation, and sale save

The foundation slice does not need the full endpoint set, but it should leave room for them without forcing a later restructure.

## Sensitive Action Gate

V1 does not use a full authentication system. The only protected behavior is the admin-only ability to mark an item as `missing`.

The design direction is:

- shared admin password stored in configuration
- lightweight in-app unlock flow
- short-lived session flag after successful unlock

The foundation slice should prepare the configuration model and supporting structure for this gate, but it does not need to finish the full reconciliation unlock flow yet.

## Data Flow

The intended steady-state flow is:

1. Intake writes authoritative seller and item records.
2. Checkout reads a lightweight catalog, validates against the live database, and saves sales transactionally.
3. Reconciliation derives seller outputs from current database facts and applies the `in_stock -> missing` admin-only transition.

The first slice only needs to make sure the code structure and persistence layer support that flow without redesign.

## Error Handling

The foundation should prefer explicit, operator-readable failures over hidden fallback behavior.

Implementation direction:

- fail startup clearly when required configuration is missing
- fail database initialization clearly when the SQLite path is invalid or inaccessible
- keep transactional workflow errors distinct from validation errors
- preserve room for the documented checkout conflict behavior without inventing implicit recovery

This matches the project’s broader failure philosophy: clear failure, manual fallback, later reconciliation.

## Testing Strategy

The first slice should include automated coverage for the rules most likely to drift from the docs:

- seller code generation order and safe alphabet
- item reference formatting
- allowed item status transitions
- EF model constraints that can be validated with integration tests

The goal is not broad test volume. The goal is to lock down the business rules that future features will rely on.

## Milestone Definition

The foundation milestone is complete when:

- the web app boots successfully
- configuration binding works for the documented sections
- the EF Core model and initial migration match the schema docs
- the SQLite connection is configured for V1 expectations
- core domain rules are implemented in code with tests
- the project has the internal structure needed for intake, checkout, and reconciliation feature slices

## Follow-On Work

After this slice, the next likely implementation order is:

1. intake foundation and seller/item creation
2. preregistration confirmation workflow
3. checkout workflow and transactional sale save
4. reconciliation views and seller-sheet generation
5. admin unlock and `missing` correction flow
