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

## D-007 Item Status Is A Single Field With Four Values

- Status: superseded by D-040 for current V1
- Key drivers:
  - the workflow needs to distinguish preregistered items from physically delivered stock
  - preregistered-but-not-delivered items do not need to remain in the operational dataset in V1
  - item deletion is already restricted to entries that are still `registered` or `in_stock`
  - separate lifecycle dimensions would still be over-engineering for V1
- Decision:
  - use a single status field with values: `registered`, `in_stock`, `sold`, `missing`
  - do not use a `not_provided` status in V1
  - delete preregistered items that were not physically delivered while they are still `registered`
- Consequence:
  - payable items are derived as `sold + missing`
  - returned items are derived as `in_stock`
  - preregistration and physical stock confirmation can be modeled without extra lifecycle tables
  - deletion remains impossible for `sold` or `missing` items in normal operations

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

## D-010 Do Not Track Payment Methods In MVP

- Status: superseded by D-035 for current V1
- Key drivers:
  - operationally only "paid" matters
  - payment method detail did not create value for the original MVP scope
- Decision:
  - the MVP tracked only that a sale was completed
- Consequence:
  - the MVP checkout flow and data model stayed simpler
  - current V1 stores payment method and cheque traceability at sale level

## D-011 Optional Print Output Was Secondary

- Status: superseded by D-042
- Key drivers:
  - most customers did not need printed sale documents under the original MVP assumptions
  - printed sale documents were primarily expected for reimbursement cases
- Decision:
  - printed sale output was optional and secondary in the checkout flow
  - use one optional `buyer_address` text field, empty by default
  - if `buyer_address` was filled, that meant a printed sale document was requested
- Consequence:
  - print logic did not complicate the main save-sale action
  - V1 did not need separate print-requested or print-completed fields

## D-012 Seller Fee Logic Stays Outside The System In MVP

- Status: superseded by D-037
- Key drivers:
  - the EUR 2 per 15 items fee can be handled outside the system for now
  - seller statement logic is simpler if it only reflects payable items and returned items
- Decision:
  - final amount to pay seller in the MVP is only the sum of seller prices for payable items
- Consequence:
  - seller fee handling was deferred without blocking the core workflow

## D-013 Association Margin Is Stored Per Item But Not Used For Seller Settlement

- Status: superseded by D-028 and D-049
- Key drivers:
  - sale price is seller price plus 20 percent
  - MVP settlement was intended to stay simple and based on seller price only
- Decision:
  - store both seller price and customer-facing sale price on each item
- Consequence:
  - settlement in that older MVP shape remained simple
  - association margin can be analyzed separately

## D-014 Use Seller-Centered Item References

- Status: superseded by D-050
- Key drivers:
  - post-sale physical triage relies on grouping seller items together
  - 26 single-letter buckets are insufficient for 100 to 200 sellers
  - the old reserved-number trick is unnecessary with per-seller numbering
- Decision:
  - use references in the form `SELLERCODEITEMNO`, e.g. `BK014`
- Consequence:
  - references naturally group items by seller
  - late-added items simply take the next number within the seller

## D-015 Seller Codes Use A Safe 2-Letter Alphabet

- Status: accepted
- Key drivers:
  - references must be easy to read and write by hand
  - ambiguous letters increase operational error risk
- Decision:
  - exclude `A`, `F`, `I`, `O`, `Q`, and `V` from seller code generation
- Consequence:
  - seller code space remains large enough for the event
  - operator confusion is reduced

## D-016 Seller Codes Are Assigned In A Spread Order

- Status: accepted
- Key drivers:
  - sellers should be spread across first letters for easier post-sale physical sorting
- Decision:
  - assign seller codes in a column-first order, e.g. `BB`, `CB`, `DB`, ..., then `BC`, `CC`, `DC`, ...
- Consequence:
  - code distribution is visually and operationally balanced

## D-017 Item Numbers Are Fixed-Width Per-Seller Sequences

- Status: superseded by D-050
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
  - support preregistration entered by operators, with later physical confirmation
  - delivered preregistered items are marked `in_stock`
  - undelivered preregistered items are deleted while still `registered`
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
  - when the operator clicks `Finaliser`, the cart is first validated against the live database
  - if validation succeeds, a finalization modal is shown with the subtotal, checkout fee, final total, item count, and the French actions `Retour`, `Enregistrer la vente`, and `Enregistrer et imprimer`
  - the modal was later extended by D-035 with a required sale-level payment method choice and cheque fields when relevant
  - `Retour` returns to the editable cart without saving
  - both `Enregistrer la vente` and `Enregistrer et imprimer` perform a second authoritative validation inside the save transaction
  - `Enregistrer et imprimer` is disabled unless `buyer_address` was filled, and it saves before opening the browser print dialog
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
    - `Printing`: print header text only
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
  - checkout pricing should stay simple and based on seller-entered item prices
- Decision:
  - store one item price in V1: `seller_price`
  - treat that item price as both the printed per-item customer price and the reconciliation payable subtotal before settlement deductions
  - compute a checkout fee equal to 10 percent of the cart subtotal
  - round the checkout fee to the nearest cent, with midpoint values rounded away from zero
  - store sale-level payment values on `sale`: subtotal, checkout fee, and total paid
  - store only `seller_price_at_sale` on each `sale_line`
- Consequence:
  - seller settlement starts from seller price snapshots and then applies the D-049 association margin and any remaining seller fee
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
  - include seller sheets only for sellers with at least one `sold`, `missing`, `in_stock`, or `defective` item
  - allow admin corrections between `in_stock` and `missing`
  - keep seller-sheet printing and `missing` corrections read-only until the event phase is `post_sale`
  - seller fee calculation was kept outside the MVP; this is superseded by D-037 for current V1
- Consequence:
  - V1 does not need a separate reconciliation executable, script, or deployment path
  - reconciliation uses the same database and status lifecycle as sale-day operations
  - checkout remains focused on sales while reconciliation has its own page and print workflow
  - production configuration must set a real admin password; blank or placeholder values are invalid
  - older documentation that described reconciliation as living outside the app is superseded by this decision

## D-031 Use Event-Level Sale Phases And Explicit Sale-Line Corrections

- Status: accepted
- Key drivers:
  - organizers need to close checkout before reconciliation without relying on convention
  - the sale day must not be blocked if admins have not formally moved from preparation to sale mode
    - sale mode should prevent unexpected seller and item master-data changes
  - a mistakenly sold item cannot safely be fixed by changing only `item.status`
- Decision:
  - persist one event-level phase in the database: `presale`, `sale`, or `post_sale`
  - default new databases to `presale`
  - allow new sale creation in `presale` and `sale`
  - block new sale validation and sale creation in `post_sale`
    - lock seller and item master data in `sale` and `post_sale`
  - expose admin phase changes through `/administration`, guarded by a session-backed `Admin:SharedPassword` unlock
  - correct a mistaken sale by marking the active `sale_line` with `corrected_at` and returning the item to `in_stock`
- Consequence:
  - `presale` is a preparation mode, not a hard "sales disabled" mode
  - `sale` is primarily a catalog lock
  - `post_sale` is the only phase that blocks new checkout persistence
  - old sale totals remain immutable after sale-line correction
  - seller sheets ignore corrected sale lines and can show the returned item from current item status

## D-032 Expose Read-Only Sales History From Checkout

- Status: accepted
- Key drivers:
  - checkout operators may need to inspect a saved sale during normal sale-day work
  - sale lookup should be reachable from the checkout area without turning the admin page into a general sales browser
  - saved sales are historical facts and should not be edited from a browse view
  - buyer address details may be useful for lookup, but V1 already treats checkout operators as trusted event staff
- Decision:
  - keep `/caisse` as the default sale-creation page
  - expose a prominent `LISTE DES VENTES` action from `/caisse`
  - add `/caisse/ventes` for read-only sales history, visible to all operators without the shared admin password
  - add `/caisse/ventes/{id}` for read-only sale detail
  - search the list by sale id, item reference, item title, buyer details, and buyer address details
  - show corrected sale lines as historical sale facts labeled `Corrigée`
  - keep sale-line correction actions only on `/administration`
- Consequence:
  - operators can resolve checkout questions without admin unlock
  - sale browsing stays visually and conceptually attached to checkout
  - the admin page remains focused on phase changes and exceptional corrections
  - historical sale totals remain immutable and visible even when a sale line was later corrected

## D-033 Require Seller Phone And Postal Address

- Status: superseded by D-045
- Key drivers:
  - event owners need reliable seller contact details before accepting items
  - phone-only contact is not enough for seller settlement and post-event follow-up
  - missing contact information should be blocked at entry instead of discovered during reconciliation
- Decision:
  - make `seller.phone` required
  - add required `seller.postal_address`
  - add required `seller.postal_code` and `seller.city`
  - enforce those contact fields in the seller create/edit UI and in backend intake validation
  - reuse the checkout postal-code city helper on seller create/edit pages
  - keep `email` optional
- Consequence:
  - existing databases receive empty default values during migration and should be completed by organizers
  - new seller records cannot be created or edited through intake without phone, postal address, postal code, and city
  - seller detail and printable seller sheets show the required contact information

## D-034 Restrict Seller Price Entry To Whole Euros

- Status: accepted
- Key drivers:
  - owner feedback after the first user showcase was that seller prices should not be entered with cents
  - the event workflow is simpler when volunteers type whole-euro seller prices only
  - the database should keep cent precision available in case the rule changes later
- Decision:
  - keep `item.seller_price_cents` as the persisted column
  - require application-created seller prices to be whole euros, stored as cent values divisible by `100`
  - reject seller item creation and item price edits when the submitted seller price contains cents
  - render seller price inputs as whole-euro text fields with numeric input mode and French custom browser validation
- Consequence:
  - checkout and reconciliation can continue reading existing cent values if they exist
  - future cents support would mainly relax application validation rather than rename price columns
  - operator-facing price entry now matches the whole-euro event rule

## D-035 Persist Sale Payment Method And Cheque Traceability

- Status: accepted
- Key drivers:
  - post-sale association banking work needs to match physical cheques with saved sales
  - checkout operators need a simple sale-finalization choice that does not slow down card or cash sales
  - payment traceability belongs to the whole sale, not individual sale lines
- Decision:
  - add the payment method choice to the `/caisse` finalization modal
  - store payment method on `sale` as `card`, `cash`, or `cheque`
  - require cheque number and bank name when the operator selects `Chèque`
  - keep cheque details nullable for non-cheque sales and for historical rows created before this feature
  - expose payment method and cheque details in `/caisse/ventes` and `/caisse/ventes/{id}`
- Consequence:
  - the sale creation API accepts `paymentMethod`, `chequeNumber`, and `chequeBankName`
  - `CheckoutService` remains the authoritative validator for cheque traceability
  - read-only sales search can find cheque sales by method, number, or bank name

## D-036 Store Structured Buyer Details On Sales

- Status: accepted
- Key drivers:
  - checkout operators need to associate a sale with a buyer name or structure without overloading the buyer address field
  - buyer location should be searchable and visible in sales history
  - city suggestions should support the local Isère workflow without adding an internet dependency during the sale
- Decision:
  - add `buyer_name`, `buyer_postal_code`, and `buyer_city` fields to `sale`
  - require buyer name, postal code, and city for new application-created checkout sales
  - render those fields at the top of `/caisse`, above item lookup
  - keep `buyer_address` as a separate optional free-text field, labeled `Adresse` in the UI
  - generate local city suggestion data for Isère (`38xxx`) from the official French Geo API commune dataset
  - prefill city when an Isère postal code maps to exactly one known city
  - keep city entry free text for other French departments
- Consequence:
  - sale creation persists buyer identity and location as structured sale-level data
  - read-only sale history can search and display buyer details directly
  - the checkout page remains usable if the city suggestion data is incomplete because the city field is still editable free text

## D-037 Track Seller Registration Fees In-App

- Status: accepted
- Key drivers:
  - V1 seller settlement must account for the EUR 2 registration fee per started block of 15 items
  - operators need to see whether the seller already paid the expected fee during intake
  - unpaid seller fees should reduce the reconciliation payout instead of being tracked on paper
- Decision:
  - store only the manually collected amount on `seller.fee_paid_cents`
  - calculate the expected fee from the seller's total item count in the system
  - charge EUR 2 for every started block of 15 items: 1-15 items costs EUR 2, 16-30 costs EUR 4, and so on
  - show expected and paid fees on seller details and collect the paid amount on seller create/edit screens
  - include item subtotal, expected fee, paid fee, remaining fee, and final payout on reconciliation seller sheets
  - subtract the remaining unpaid fee from the seller payout, floored at zero
- Consequence:
  - `D-012` is superseded for current V1
  - the expected fee remains derived and cannot drift from the item count
  - overpayment is visible as paid fee but never increases the seller payout

## D-038 Expose Ungated Read-Only Data Lists

- Status: accepted
- Key drivers:
  - organizers need a broad operational browser for items, sellers, and sales without entering a correction workflow
  - existing screens are optimized for specific tasks and sometimes hard-filter records by workflow status
  - the view should help inspection without creating another place where event data can be changed
- Decision:
  - expose a French read-only data browser at `/donnees`
  - add a top-level `DONNÉES` navigation entry
  - keep the feature available to operators without the shared admin password
  - use initial tabs `Articles`, `Vendeurs`, `Ventes`, and `Chèques`; D-045 later adds the read-only `Clôture` tab
  - list all item statuses on the `Articles` tab and provide a status filter
  - search sellers only by seller code and seller name
  - search sales only by sale id, buyer details, payment method, item reference, and item title
  - list registered cheques on a dedicated tab by sale id, cheque number, bank name, and sale total amount
  - do not expose edit, correction, deletion, phase, or reconciliation actions from this page
- Consequence:
  - the data browser complements the focused checkout, intake, and reconciliation pages instead of replacing them
  - sale lookup in `/donnees` is intentionally narrower than `/caisse/ventes`, which can still search invoice and cheque traceability details
  - future additions to the data browser should stay read-only unless a separate decision creates an admin workflow there

## D-039 Treat Seller Excel Files As Editable Intake Prefill

- Status: accepted
- Key drivers:
  - seller-provided Excel files should reduce manual typing without bypassing operator review
  - the accepted workbook template describes one seller and one item list, not a bulk multi-seller import
  - seller code and item reference generation must remain server-side and transactional
  - duplicate seller detection must prevent Excel intake from creating a second seller record
- Decision:
  - expose Excel upload from `/vendeurs/creer` as a prefill helper
  - accept only one `.xlsx` workbook with one visible seller worksheet for this slice, ignoring hidden helper worksheets
  - read item titles and seller prices from columns `B:C`, continuing below the old 15-row template limit until the first blank item row
  - fill the editable seller and item form during upload but do not persist seller or item data
  - save manually entered or Excel-prefilled seller and item data through one normal intake action and one database transaction
  - redirect duplicate sellers to the existing seller detail page and prefill the parsed rows into the add-items form for review
  - leave Excel-prefilled seller contact fields blank when the workbook does not provide them, and use `EUR 0` for paid seller fee until those workbook fields are formally designed
  - read Excel `CP VILLE` when present, accepting only canonical postal-code/city pairs from the authoritative Isere city list used by the application
  - generate the Excel workbook's hidden `codes postaux` list from `wwwroot/data/isere-postal-cities.json`
- Consequence:
  - Excel parsing remains reversible because operators can correct the form before saving
  - the authoritative save path is shared by manual and Excel-assisted intake
  - imported rows do not need a staging table or stored workbook copy
  - existing seller details are not silently updated from Excel files
  - the Excel template and the application share the same canonical city values; unsupported mismatches remain visible during upload instead of being guessed silently

## D-040 Track Defective Items And Refunds

- Status: accepted
- Key drivers:
  - defects can be discovered after an item has already been accepted into association stock
  - defective items must not be sold, must be returned to the seller, and must not be paid to the seller
  - buyer-return defects require a full buyer refund including the 10 percent association fee
  - the original sale remains a historical checkout fact and should not be recalculated after a refund
- Decision:
  - extend `item.status` with `defective`
  - keep defects found before physical acceptance outside the database, except that a preregistered `registered` row may be deleted
  - keep `in_stock -> defective` as an admin task from `Correction des articles` on `/administration`, not from the seller item list
  - let admins restore `defective -> in_stock` only when no buyer refund history exists
  - repurpose the admin correction section as `Correction des articles`, loading non-`registered` items into the page and filtering them client-side by the operator's search text
  - handle buyer-return defects from `/administration` with a `Rembourser` action on sold items only
  - record defective refunds directly on `sale_line` with `refunded_at`, refunded item price, refunded checkout fee, total refund, and `refund_reason = defective`
  - allow admins to cancel a defective refund that was entered by mistake, clearing the sale-line refund fields and restoring the item to `sold`
  - treat active payable sale lines as `corrected_at IS NULL AND refunded_at IS NULL`
  - include `defective` items in seller-sheet returned items with a visible `DÉFECTUEUX` marker
  - exclude refunded sale lines and defective items from seller payout
- Consequence:
  - the admin page covers phase changes, item correction, mistaken sale correction, rare buyer-return refunds, and refund cancellation for operator mistakes
  - read-only sale history shows refunded defective lines separately from corrected lines, then shows `Remboursements` and `Total corrigé` in the totals block
  - seller settlement remains based on payable sold and missing items only
  - a future richer accounting model can promote refunds to a separate table if needed, but V1 keeps the lightweight sale-line correction pattern

## D-041 Use A General Administration Route

- Status: accepted
- Key drivers:
  - the former sale-status page now handles event phase changes, mistaken sale corrections, defective item marking, defective refunds, and refund cancellation
  - the previous sale-status route no longer describes the scope of the page
  - operators need one obvious admin entry point rather than several subtly different correction pages
- Decision:
  - make `/administration` the user-facing admin route and main navigation target
  - keep the page title as `Administration`
  - keep the sale phase controls as a section named `Statut de la vente`
- Consequence:
  - future exceptional item workflows should live under `/administration` when they require the shared admin unlock
  - docs and operator guidance should refer to `/administration`

## D-042 Replace Optional Print Output With Invoice Output

- Status: accepted
- Key drivers:
  - user feedback identified mandatory invoice fields for printed checkout documents
  - the association is exempt from VAT under article 293 B of the CGI
  - invoice numbers must be unique, chronological, and continuous for the event numbering period
- Decision:
  - use `Facture` for operator-facing wording and `Invoice` for new internal concepts
  - create invoice data only from `Enregistrer et imprimer`; normal sale saves do not allocate an invoice number
  - persist nullable `sale.invoice_number` and `sale.invoice_issued_at`
  - use configured invoice numbering in the `YYMMAFBJNNNN` format, for example `2611AFBJ0001`
  - treat `buyer_address` as the customer address printed on the invoice, with no separate billing address
  - print the invoice title as `Facture <invoice number>` and use the AFSE logo asset
  - print one customer block containing buyer identity, customer address, postal code, and city beside the seller identity block
  - print the Association Familiale seller identity without a `Statut :` label
  - print item quantities/designations, compact `P.U. HT` prices, TVA as zero, TTC total, and the VAT exemption text once per invoice
  - hide the application event header and navigation while printing the invoice
- Consequence:
  - the former optional print output is superseded by invoice output
  - invoice sequencing is server-side and happens inside the successful sale transaction
  - reprint support, if added later, must reuse the persisted invoice number and issue date

## D-043 Validate Deposits At Seller Level

- Status: accepted
- Key drivers:
  - physical handover is proven by a printed paper contract signed by the seller
  - operators need one obvious action for the whole seller deposit, not per-item stock toggles
  - item data may be incomplete before signature, but normal seller/item edits must stop after the signed contract is accepted
  - the paper contract is sufficient legal proof for V1; the database only needs an operational gate
- Decision:
  - add `seller.deposit_status` with `deposit_pending` and `deposit_validated`
  - create normal intake and Excel-prefilled items as `registered`, including in-person entry
  - print the seller contract recap from the seller detail page with association identity, legal mentions, seller fee status, current item prices, and only the seller signature area
  - require complete seller contact information before deposit validation, not before draft seller creation or edit
  - validate the seller deposit only after the operator confirms `Le contrat est signé`
  - on validation, move the seller's accepted `registered` items to `in_stock` and lock normal seller/item edits for that seller
  - remove the normal UI path for manual item-level `registered -> in_stock`
  - gate checkout catalog and sale save on both `item.status = in_stock` and `seller.deposit_status = deposit_validated`
  - allow an admin to cancel a seller deposit validation from the seller page during `presale` after entering the shared admin password
  - cancellation sets the seller back to `deposit_pending` and moves only items still `in_stock` back to `registered`
  - cancellation and revalidation tolerate historical `sold`, `missing`, and `defective` items, which stay unchanged and read-only
  - do not store a contract number, signed timestamp, or database snapshot in V1
- Consequence:
  - one signed deposit corresponds to one seller-level validation
  - the signed paper remains the legal artifact; the app status prevents premature checkout and later accidental edits
  - sellers can remain partially entered as `deposit_pending` until the operator is ready to print and sign the contract

## D-044 Print Invoices From Read-Only Sales

- Status: accepted
- Key drivers:
  - operators may need to print a facture after the sale has already been saved
  - invoice printing must not introduce a new customer address after the sale
  - reprints must not consume additional invoice numbers
  - the existing read-only sale route should remain the single sale-detail URL
- Decision:
  - keep `/caisse/ventes/{id}` as the single read-only sale detail route
  - show `Imprimer la facture` on `/caisse/ventes/{id}` only when `sale.buyer_address` was captured on the original sale
  - if the sale already has `invoice_number` and `invoice_issued_at`, reuse them for printing
  - if the sale has `buyer_address` but no invoice number yet, allocate the next configured invoice number server-side, persist `invoice_issued_at`, then open the print dialog
  - use the same print-only invoice structure as checkout: AFSE logo, seller/client blocks, compact line table, HT/TVA/TTC totals, and one VAT exemption mention
- Consequence:
  - past-sale invoice printing stays tied to data captured at sale time
  - a saved sale can receive its first invoice number after checkout, but only once and only when the address already exists
  - `/vente/{id}` is not added as a second route, avoiding duplicate sale-detail URLs

## D-045 Relax Draft Seller Contact Requirements

- Status: accepted
- Key drivers:
  - Excel and CSV prefill can contain useful seller and item data even when contact details are incomplete
  - organizers can complete missing seller details more efficiently when the seller physically deposits items
  - the signed deposit validation is already the operational gate that moves items into stock and locks normal edits
  - a seller without a phone cannot be reliably distinguished from another seller with the same name
- Decision:
  - allow `deposit_pending` sellers to be created and edited with blank phone, postal address, postal code, and city
  - keep first name and last name required for seller creation and duplicate detection
  - when the submitted phone is blank, block duplicate sellers on normalized first name and last name only
  - do not use placeholder contact values such as `UNKNOWN` for new Excel-prefilled data
  - require phone, postal address, postal code, city, identity document fields, and complete item rows before seller deposit validation
- Consequence:
  - incomplete imported seller records can be saved as drafts instead of being rejected or filled with fake data
  - checkout remains protected because only validated deposits move items to `in_stock`
  - operators must complete missing contact and identity document information before confirming `Le contrat est signé`

## D-046 Import External Seller Form Exports As Drafts

- Status: accepted
- Key drivers:
  - organizers can collect preregistration data through an external no-paid-third-party form while keeping the sale tool private on the local network
  - form exports can contain useful seller and item data even when contact details, city spelling, or item lines are incomplete
  - the seller deposit validation gate already prevents incomplete imported sellers from reaching checkout stock
  - non-technical operators need a direct import result, not a complex row-by-row correction center
- Decision:
  - expose `/vendeurs/importer` for CSV and TSV seller form exports
  - detect provider metadata rows before the real header row, including Framaforms-style exports
  - import non-duplicate rows with first name and last name as `deposit_pending` sellers
  - create valid parsed item lines as `registered` items and ignore invalid item lines
  - add nullable `seller.import_notes` for durable import warnings
  - when a submitted city cannot be reconciled, keep the submitted postal code, leave `seller.city` blank, and record the submitted city in `seller.import_notes`
  - do not auto-import duplicate seller rows
  - when exactly one duplicate seller matches, allow the operator to append valid new item lines to that existing seller without overwriting seller contact fields
  - when appending to an existing seller, ignore imported item lines whose trimmed title and seller price exactly match an existing item for that seller
  - when several duplicate seller matches exist, expose only `Ignorer cette soumission`
- Consequence:
  - external form import becomes a bulk draft creation tool, not an authoritative validation workflow
  - import notes must be reviewed on the seller detail page and can be cleared or edited from seller edit
  - seller identity/contact corrections still happen through the normal seller page before contract signing

## D-047 Require Seller Identity Document Fields Before Deposit Validation

- Status: accepted
- Key drivers:
  - the signed deposit contract is the legal artifact for accepting a seller's items
  - organizers need identity document details on that contract before items become sellable stock
  - draft seller intake, Excel prefill, and external form import may not have those details yet
- Decision:
  - store nullable seller identity document fields: document nature, document number, issue date, and issuing authority
  - keep those fields optional while the seller remains `deposit_pending`
  - require all four identity document fields before `Valider le dépôt` can set `seller.deposit_status = deposit_validated`
  - show the fields on seller create, edit, detail, and printed deposit contract surfaces
  - keep the requirement in application validation rather than a database `CHECK`, because incomplete drafts are valid records
- Consequence:
  - imported and partially entered sellers can still be saved as drafts
  - checkout remains protected because incomplete sellers cannot pass the deposit gate
  - the printed contract includes the legal identity-document details collected at deposit time

## D-048 Track Configured Caisses For Closing

- Status: accepted
- Key drivers:
  - sale-day operations may use multiple physical checkout stations
  - offline validation compares cash/card/cheque reality with system totals after the event
  - refunding a defective sold item can happen at a different checkout from the original sale
  - the `fond de caisse` is physically placed in each caisse before sales start and should be included in expected cash totals
  - operators need a low-friction caisse choice on `/caisse`
- Decision:
  - add a `cash_register` table configured from `/administration`
  - store a required `sale.cash_register_id` on every new sale
  - let the checkout page keep the selected caisse id in browser `localStorage` so one physical station can keep using the same caisse across reloads
  - when refunding a defective sale line from `/administration`, require the admin to choose the configured refund caisse that physically paid the refund
  - store the selected refund caisse on `sale_line.refund_cash_register_id`
  - add a read-only `/donnees?onglet=cloture` tab named `Clôture`
  - show one global row and one row per configured caisse with sales, item count, subtotal, association fee, payment-method totals, collected total, refunded total, net total, fond de caisse, and expected cash
  - group per-caisse sales by `sale.cash_register_id` and per-caisse refunds by `sale_line.refund_cash_register_id`
  - calculate expected cash per caisse as `fond de caisse + cash sales - refunds paid`
- Consequence:
  - organizers can close each physical caisse independently while still seeing the global event total
  - refund attribution follows the physical checkout that paid the refund, not the original sale checkout
  - the feature stays a lightweight cash-management aid rather than a full accounting ledger or payment-provider reconciliation system

## D-049 Apply Association Margin To Seller Settlement

- Status: accepted
- Key drivers:
  - the association keeps a 15 percent margin on payable seller item totals
  - treasurers need printable seller sheets to show the deduction before writing checks
  - seller fee deduction must continue to apply after margin deduction
- Decision:
  - calculate a 15 percent association margin from the payable item subtotal during reconciliation
  - round the margin to the nearest cent with the same away-from-zero rule used by checkout percentage fees
  - subtract both the association margin and remaining unpaid seller fee from the seller payout
  - floor the final payout at zero
  - show the margin as a separate row on printable seller sheets
- Consequence:
  - seller payout is now `payable item subtotal - association margin - remaining seller fee`
  - overpaid seller fees still never increase payout
  - refunded or corrected sold items remain excluded from payable subtotal

## D-050 Allocate Letter-Bucket Item References At Deposit

Item references are `<LETTER><NNN>` allocated when a seller's deposit reaches
the contract-print step, not derived from the seller code at item creation.

This lets the association print all adhesive label sheets before any seller is
known, which is the only way the labels can exist on the day.

The letter is a physical sort bucket for post-sale recollection, not a seller
identifier. Volunteers sort unsold items by letter then number into twenty
piles, so the letters must stay balanced; balance falls out of the existing
spread order in `SellerCodeGenerator`, whose first letter cycles every twenty
sellers.

Allocating at deposit rather than at item creation removes the need for a
numbering buffer: by the time references are assigned, the seller's item list
is final, so each seller's run is contiguous by construction.

A server-side guard in `IntakeService.ValidateSellerDepositAsync` refuses to
move a seller's `registered` items to `in_stock` while any of them still has a
null reference, since allocation and that status transition live in two
different page handlers and would otherwise be sequenced only by client-side
convention.

Supersedes [D-014](#d-014-use-seller-centered-item-references) and
[D-017](#d-017-item-numbers-are-fixed-width-per-seller-sequences).
