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

## D-008 Checkout Uses Typed Reference Search With Autocomplete

- Status: accepted
- Key drivers:
  - volunteers do not need seller context
  - exact-code-only entry would be more stressful and error-prone
  - barcode scanning is not required in V1
- Decision:
  - operators type a reference, see matching results, and explicitly select the intended item
- Consequence:
  - sale-day UI centers on a large autocomplete lookup component

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
- Consequence:
  - receipt logic should not complicate the main save-sale action

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

- Status: accepted
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
