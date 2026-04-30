# Decisions

This file captures the main project decisions in a lightweight ADR format.

## D-001 Build A Custom Tool Rather Than Centering On Odoo

- Status: accepted
- Key drivers:
  - the difficult part is end-of-day reconciliation, not generic POS behavior
  - sale-day UI must be much simpler than a standard ERP/POS UI
  - Odoo would introduce conceptual mismatch around products, sellers, and settlement
- Decision:
  - build a small custom event tool with a central database and purpose-built flows
- Consequence:
  - more custom implementation work up front
  - better fit for event-specific operations

## D-002 Split The System Into Two Independent Features

- Status: accepted
- Key drivers:
  - sale-day operations and post-sale reconciliation have different users and different UX needs
  - reconciliation does not need to run during the sale
- Decision:
  - treat event operations and post-sale reconciliation as separate features over the same database
- Consequence:
  - each feature can be implemented and evolved independently
  - no need to build a large unified admin application in V1

## D-003 Use A Single Central Server And Database

- Status: accepted
- Key drivers:
  - need one authoritative source of truth during the event
  - avoid end-of-day consolidation across multiple station databases
  - keep the architecture simple and auditable
- Decision:
  - deploy one central server and one central database on the local network
- Consequence:
  - server failure stops digital workflow
  - architecture remains much simpler than a distributed or offline-first system

## D-004 No Offline Sync Or Client Replay Logs In V1

- Status: accepted
- Key drivers:
  - offline/replay logic would create merge, duplication, and conflict complexity
  - hard-stop plus paper fallback is acceptable operationally
- Decision:
  - do not implement local per-station logs or replay in V1
- Consequence:
  - server outage requires switching to manual paper bills
  - V1 stays small and operationally predictable

## D-005 Paper Fallback Is The Official Failure Strategy

- Status: accepted
- Key drivers:
  - volunteers need a clear degraded-mode procedure
  - building digital continuity is too expensive for V1
- Decision:
  - if the server is unavailable, switch to numbered paper bills and perform manual reconciliation later
- Consequence:
  - resilience depends on operational discipline rather than software complexity

## D-006 One Physical Toy Equals One Item Record

- Status: accepted
- Key drivers:
  - each toy needs its own reference and status
  - payout and return logic work at item level
- Decision:
  - model each physical toy as an independent item record
- Consequence:
  - lookup, sale, and reconciliation stay deterministic

## D-007 Item Status Is A Single Field With Five Values

- Status: accepted
- Key drivers:
  - the workflow needs to distinguish preregistered items from physically delivered stock
  - preregistered-but-not-delivered items should not be deleted silently
  - separate lifecycle dimensions would still be over-engineering for V1
- Decision:
  - use a single status field with values: `registered`, `in_stock`, `not_provided`, `sold`, `missing`
- Consequence:
  - payable items are derived as `sold + missing`
  - returned items are derived as `in_stock`
  - preregistration and physical stock confirmation can be modeled without extra lifecycle tables

## D-008 Checkout Uses A Live-Filtered Reference And Title Lookup Table

- Status: accepted
- Key drivers:
  - volunteers do not need seller context
  - exact-code-only entry would be more stressful and error-prone
  - barcode scanning is not required in V1
- Decision:
  - operators type into a text field that live-filters a visible results table
  - matching is done against `reference + title`
  - operators explicitly select an item from the filtered table
- Consequence:
  - sale-day UI centers on a large live-filtered lookup table rather than an autocomplete widget

## D-009 Do Not Show Seller Information During Checkout

- Status: accepted
- Key drivers:
  - seller context is irrelevant during the sale
  - checkout must remain simple and uncluttered
- Decision:
  - hide seller information from the sale-day flow
- Consequence:
  - seller logic stays in intake and reconciliation only

## D-010 Do Not Track Payment Methods In V1

- Status: accepted
- Key drivers:
  - operationally only "paid" matters
  - payment method detail does not create value for the event in V1
- Decision:
  - track only that a sale was completed
- Consequence:
  - simpler checkout flow and simpler data model

## D-011 Receipts Are Optional And Rare

- Status: accepted
- Key drivers:
  - most customers do not need receipts
  - receipts are primarily for reimbursement cases
- Decision:
  - receipt printing is optional and secondary in the checkout flow
  - use one optional `receipt_details` text field, empty by default
  - if `receipt_details` is filled, that means a receipt was requested
- Consequence:
  - receipt logic should not complicate the main save-sale action
  - V1 does not need separate `receipt_requested` or `receipt_printed` fields

## D-012 Seller Fee Logic Stays Outside The System In V1

- Status: accepted
- Key drivers:
  - the EUR 2 per 15 items fee can be handled outside the system for now
  - seller statement logic is simpler if it only reflects payable items and returned items
- Decision:
  - final amount to pay seller in V1 is only the sum of seller prices for payable items
- Consequence:
  - seller fee handling is deferred without blocking the core workflow

## D-013 Association Margin Is Stored Per Item But Not Used For Seller Settlement

- Status: superseded by D-028
- Key drivers:
  - sale price is seller price plus 20 percent
  - seller settlement should stay simple and based on seller price only
- Decision:
  - store both seller price and customer-facing sale price on each item
- Consequence:
  - seller settlement remains simple
  - association margin can be analyzed separately

## D-014 Use Seller-Centered Item References

- Status: accepted
- Key drivers:
  - post-sale physical triage relies on grouping seller items together
  - 26 single-letter buckets are insufficient for 100 to 200 sellers
  - the old reserved-number trick is unnecessary with per-seller numbering
- Decision:
  - use references in the form `SELLERCODE-ITEMNO`, e.g. `BK-014`
- Consequence:
  - references naturally group items by seller
  - late-added items simply take the next number within the seller

## D-015 Seller Codes Use A Safe 2-Letter Alphabet

- Status: accepted
- Key drivers:
  - references must be easy to read and write by hand
  - ambiguous letters increase operational error risk
- Decision:
  - exclude `I`, `O`, `Q`, and `V` from seller code generation
- Consequence:
  - seller code space remains large enough for the event
  - operator confusion is reduced

## D-016 Seller Codes Are Assigned In A Spread Order

- Status: accepted
- Key drivers:
  - sellers should be spread across first letters for easier post-sale physical sorting
- Decision:
  - assign seller codes in a column-first order, e.g. `AA`, `BA`, `CA`, ..., then `AB`, `BB`, `CB`, ...
- Consequence:
  - code distribution is visually and operationally balanced

## D-017 Item Numbers Are Fixed-Width Per-Seller Sequences

- Status: accepted
- Key drivers:
  - fixed-width numbering improves readability and sorting
- Decision:
  - use per-seller item numbers such as `001`, `002`, `003`
- Consequence:
  - references sort predictably
  - late-added items remain simple to assign

## D-018 Seller Preregistration Is Operator-Managed In V1

- Status: accepted
- Key drivers:
  - sellers may preregister by email before physically bringing toys
  - some sellers still register in person with toys already present
  - seller self-service remains out of scope for V1
- Decision:
  - support preregistration entered by operators, with later physical confirmation and a simple `Check all` helper for delivery review
- Consequence:
  - V1 supports both email preregistration and in-person registration without adding a seller portal

## D-019 Sensitive Reconciliation Actions Use A Shared Admin Password

- Status: accepted
- Key drivers:
  - marking an item as `missing` changes seller payout and should not be available to every operator
  - V1 does not need a full user-account or identity system
  - the real goal is misuse prevention in operations, not strong security
- Decision:
  - protect `missing` changes with a shared admin password
  - treat "admin" as an operational capability in V1, not as a named software identity
- Consequence:
  - non-admin operators cannot perform the `in_stock -> missing` transition in the normal workflow
  - V1 avoids a broader authentication and authorization system
  - the control is intentionally lightweight and should not be described as full security or auditability

## D-020 Use SQLite As The V1 Database Engine

- Status: accepted
- Key drivers:
  - V1 has one central server and a small number of client stations
  - expected checkout write transactions are short
  - operational simplicity matters more than database-server features
  - moving the system between pre-sale and sale-day machines is easier with a database file than with a separately managed database service
- Decision:
  - use SQLite as the V1 production database engine
  - keep the database file on the server's local disk only
  - enable `WAL` mode and foreign-key enforcement
  - store all money values as integer cents
  - store timestamps in UTC with one consistent format
- Consequence:
  - deployment and machine transfer stay simple
  - write concurrency remains limited compared with a server database, but is acceptable for the expected event load
  - the application must keep sale write transactions short and handle brief lock contention cleanly
  - the active implementation and documentation path stays focused on one production database engine

## D-021 Use .NET 8 LTS And EF Core With Code-First Migrations

- Status: accepted
- Key drivers:
  - the backend stack should match the developer's strongest platform knowledge
  - the implementation should follow a conventional and agent-friendly .NET path
  - the schema will likely evolve during early development and needs a clear migration workflow
- Decision:
  - use .NET 8 LTS as the application runtime baseline
  - use ASP.NET Core with Razor Pages
  - use EF Core as the default data access layer
  - use code-first migrations for schema evolution
- Consequence:
  - implementation has a clearer standard structure for agents and future maintainers
  - database changes can be tracked through normal EF Core migration workflows
  - lower-level data access approaches such as Dapper or raw SQL remain possible for isolated cases later, but are not the default architecture

## D-022 Use One Database File Per Event And Remove Event Data From The Relational Schema

- Status: accepted
- Key drivers:
  - operational clarity matters more than theoretical multi-event flexibility in V1
  - yearly events should not create noise in seller and item lists inside one database
  - event selection and event scoping would add avoidable application complexity
- Decision:
  - use one database file per event
  - remove the relational `event` table and all `event_id` relations from the V1 schema
  - keep event metadata such as the event name or year code in application configuration
- Consequence:
  - the V1 schema is smaller and more directly aligned with the operational model
  - the UI does not need event management or current-event selection behavior
  - a future multi-event schema remains possible through later migration if it becomes valuable

## D-023 Run The App As A Self-Hosted Kestrel Executable In V1

- Status: accepted
- Key drivers:
  - deployment should be as simple as possible for a local-network internal tool
  - V1 does not need IIS or Windows Service complexity up front
- Decision:
  - run the application as a self-hosted ASP.NET Core executable using Kestrel only in V1
- Consequence:
  - startup and deployment stay simple
  - future hosting changes remain possible if operational needs change

## D-024 Use A Single Web Project With Internal Folders

- Status: accepted
- Key drivers:
  - the app is small and should avoid unnecessary solution complexity
  - the implementing agent benefits from a conventional but lightweight structure
- Decision:
  - use one web project with internal folders such as `Controllers`, `Pages`, `Domain`, and `Infrastructure`
- Consequence:
  - the solution stays easier to navigate at V1 size
  - project boundaries can be split later if real complexity emerges

## D-025 Use A Two-Stage Checkout Finalization Flow

- Status: accepted
- Key drivers:
  - operators need one last clear chance to review the cart before the sale is committed
  - the UI should detect stale cart conflicts early without sacrificing authoritative save-time validation
  - printing must not create ambiguity about whether a sale was persisted
- Decision:
  - when the operator clicks `Finalize`, the cart is first validated against the live database
  - if validation succeeds, a finalization modal is shown with the subtotal, checkout fee, final total, item count, and the actions `Go back`, `Save`, and `Save and print receipt`
  - `Go back` returns to the editable cart without saving
  - both `Save` and `Save and print receipt` perform a second authoritative validation inside the save transaction
  - `Save and print receipt` is disabled unless `receipt_details` was filled, and it saves before opening the browser print dialog
- Consequence:
  - operators get a safer and clearer last-step review flow
  - sale correctness still depends on the authoritative server-side save validation
  - the checkout page resets to a fresh state and reloads its in-memory catalog after successful save actions

## D-026 Use appsettings.json As The V1 Configuration Source

- Status: accepted
- Key drivers:
  - configuration should stay simple and familiar for a self-hosted Windows deployment
  - the implementing agent needs one obvious configuration source
- Decision:
  - use `appsettings.json` as the primary configuration source in V1
  - use the sections `Database`, `Event`, `Admin`, and `Printing`
  - do not create a `Paths` section in V1
  - keep the section contents minimal:
    - `Database`: SQLite file path only
    - `Event`: event name only
    - `Admin`: shared admin password only
    - `Printing`: receipt header text only
- Consequence:
  - the agent has one small and explicit configuration contract to implement
  - runtime behavior such as SQLite pragmas and retry policies should be handled in application code rather than as V1 config switches

## D-027 Use French For The User-Facing UI And Routes

- Status: accepted
- Key drivers:
  - all operators are French-speaking
  - English labels and URLs add avoidable friction during event operations
  - V1 does not need a multilingual UI framework
- Decision:
  - use French for all operator-facing UI labels, titles, helper text, validation messages, and user-facing URLs
  - keep internal code identifiers and canonical technical documentation in English
- Consequence:
  - Razor Pages should use explicit French route templates where needed
  - the UI contract is French even though the implementation language remains English

## D-028 Model The Association Fee As A Sale-Level 10 Percent Surcharge

- Status: accepted
- Key drivers:
  - the seller-entered item price is the printed price the customer sees on each toy
  - the association fee is applied to the customer checkout total rather than baked into each item price
  - seller settlement should stay simple and based on seller prices only
- Decision:
  - store one item price in V1: `seller_price`
  - treat that item price as both the seller payout price and the printed per-item customer price
  - compute a checkout fee equal to 10 percent of the cart subtotal
  - round the checkout fee to the nearest cent, with midpoint values rounded away from zero
  - store sale-level payment values on `sale`: subtotal, checkout fee, and total paid
  - store only `seller_price_at_sale` on each `sale_line`
- Consequence:
  - seller settlement remains based on seller price only
  - customer-paid money is audited at the sale level rather than through a second per-item price
  - intake no longer needs to compute or show a derived per-item sale price

## D-029 Use Resource-Oriented API Endpoints For Interactive Checkout Actions

- Status: accepted
- Key drivers:
  - checkout is the only V1 page with substantial client-side state and asynchronous interaction
  - `technologies.md` defines a page-oriented HTML app with a narrow `/api/...` surface for interactive operations
  - validation and sale save are resource actions, not reusable page rendering concerns
  - the app should not become broadly API-first just because checkout uses JavaScript
- Decision:
  - keep `/caisse` as the Razor Page that owns checkout page structure and initial rendering
  - expose checkout validation and sale creation through narrow resource-oriented API endpoints
  - use `POST /api/caisse/validation` for pre-finalize cart validation
  - use `POST /api/ventes` for sale creation
  - keep the API controllers thin; business rules and persistence stay in `CheckoutService`
- Consequence:
  - checkout JavaScript calls explicit API resources instead of Razor Page handlers
  - the API surface remains intentionally small and driven by sale-day UI needs
  - future checkout API additions should be justified by real interactive workflow needs

## D-030 Implement Post-Sale Reconciliation Inside The Web App

- Status: accepted
- Key drivers:
  - reconciliation is an operational admin workflow, not just an offline reporting task
  - volunteers print seller sheets once, reconcile physical stock, and route missing-item corrections to admins before seller settlement
  - admins need to search by item reference or title and correct item status against the authoritative event database
  - a separate script or app would add deployment, access control, and context-switching cost without improving the V1 workflow
- Decision:
  - implement post-sale reconciliation as an admin-protected feature in the ASP.NET Core web app
  - expose a French reconciliation dashboard at `/rapprochement`
  - expose printable seller sheets at `/rapprochement/feuilles`
  - protect the whole reconciliation area with the shared `Admin:SharedPassword`
  - include seller sheets only for sellers with at least one `sold`, `missing`, or `in_stock` item
  - allow admin corrections between `in_stock` and `missing`
  - keep seller fee calculation outside the system in V1
- Consequence:
  - V1 does not need a separate reconciliation executable, script, or deployment path
  - reconciliation uses the same database and status lifecycle as sale-day operations
  - checkout remains focused on sales while reconciliation has its own page and print workflow
  - production configuration must set a real admin password; blank or placeholder values are invalid
  - older documentation that described reconciliation as living outside the app is superseded by this decision
