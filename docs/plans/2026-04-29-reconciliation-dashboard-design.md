# Post-Sale Reconciliation Dashboard Design

## Goal

Build the post-sale reconciliation workflow inside the web application as an admin-gated dashboard.

The workflow must support printing seller sheets, correcting missing-item status during physical stock gathering, and reprinting a corrected seller sheet before seller payout.

## Scope

This slice includes:

- a reconciliation dashboard at a French route such as `/rapprochement`
- one shared admin password gate for the whole reconciliation dashboard
- printing all active seller sheets in one action
- printing one seller sheet by seller code
- finding an item by reference or title during reconciliation
- changing an item from `in_stock` to `missing`
- changing an item from `missing` back to `in_stock` as an admin correction
- reprinting the seller sheet for the corrected item's seller

This slice does not include:

- per-user authentication
- audit trails for reconciliation corrections
- seller fee automation
- accounting export
- check writing inside the system
- storing generated seller sheets as database records

## Operational Workflow

After the sale is closed, an admin opens the reconciliation page and enters the shared admin password.

The admin prints all seller sheets once. A seller sheet is generated only for sellers with at least one item whose status is:

- `sold`
- `missing`
- `in_stock`

Each sheet is handed to volunteers. Volunteers gather the physical `in_stock` items listed on the sheet.

If all returned items are found, the sheet goes directly to the treasurer. The treasurer writes the seller check based on the sheet total, then the check, toys, and sheet are grouped for seller pickup.

If one or more returned items cannot be found, the sheet goes back to an admin. The admin uses the reconciliation dashboard to find each item, marks it `missing`, and prints the updated seller sheet. The corrected sheet then goes to the treasurer.

If an admin marks an item missing by mistake, the admin may change it back from `missing` to `in_stock` during reconciliation.

## Architecture

Reconciliation remains a separate feature, but it lives inside the same ASP.NET Core Razor Pages application and uses the same SQLite database.

The existing empty `ReconciliationService` becomes the business boundary for:

- loading active sellers for sheet generation
- building seller sheet view models
- finding item correction candidates
- applying admin-only reconciliation status changes

Razor Pages own the HTML and print views. The dashboard stays server-rendered because reconciliation does not need the highly interactive checkout API style.

## Routes

The user-facing routes are French:

- `/rapprochement` for the dashboard
- `/rapprochement/feuilles` for printable seller sheets

The printable sheet route may accept an optional seller code query string:

- `/rapprochement/feuilles`
- `/rapprochement/feuilles?codeVendeur=AA`

Without a seller code, the print view renders all active seller sheets. With a seller code, it renders only that seller's sheet.

## Admin Gate

The whole reconciliation page is protected by the shared admin password.

V1 does not add identity or roles. A simple session-backed gate is enough:

1. an unauthenticated admin visiting `/rapprochement` sees a password form
2. a valid password unlocks reconciliation pages for the current browser session
3. invalid passwords show a French validation message

The gate protects both the dashboard and printable sheet route.

## Seller Sheet Content

Each seller sheet is a printable document with one seller per printed page.

It contains:

- seller code
- seller name
- optional seller contact details
- payable items
- returned items
- total amount to pay the seller

Payable items include:

- `sold`
- `missing`

Returned items include:

- `in_stock`

Excluded items are not printed by default:

- `registered`

For sold items, the payable amount uses `sale_line.seller_price_at_sale_cents` because that is the immutable sale-time snapshot. For missing items, the payable amount uses `item.seller_price_cents` because the item was not sold.

Missing items must be visibly marked on the payable list.

## Printing Behavior

The dashboard has a section named `Feuilles vendeurs` with:

- a button labeled `Imprimer toutes les feuilles`
- a seller code input labeled `Code vendeur`
- a button labeled `Imprimer une feuille`

The print view uses CSS page breaks so browser printing produces one seller per sheet.

The print view should be read-only and optimized for paper, not for on-screen management.

## Missing-Item Correction

The dashboard has a section named `Correction des articles`.

The admin can search by item reference or title. Results show:

- reference
- title
- seller code
- seller name
- status
- seller price

For an `in_stock` item, the action is:

- `Marquer manquant`

For a `missing` item, the action is:

- `Remettre en stock`

The correction form should require a deliberate confirmation button but should not ask for the password again because the whole page is already admin-gated.

After a successful correction, the page shows a French success message and a direct action to print that seller's corrected sheet.

## Data Rules

Normal V1 lifecycle rules still apply:

- checkout is the only normal path from `in_stock` to `sold`
- reconciliation is the only normal path from `in_stock` to `missing`
- `registered` and `sold` are not editable from the reconciliation dashboard

The dashboard adds one explicit admin correction:

- `missing -> in_stock`

This correction exists only to undo a reconciliation mistake. It does not reopen sold items and does not allow item master data editing.

## Error Handling

The dashboard should show French validation messages for:

- invalid admin password
- unknown seller code
- seller with no active reconciliation items
- empty item search
- no item search results
- attempts to mark an item whose current status is no longer eligible

Status changes must reload the item from the database before applying the transition, so stale pages fail clearly instead of applying an invalid correction.

## Testing

Tests should cover:

- active seller filtering includes only sellers with at least one `sold`, `missing`, or `in_stock` item
- seller sheet totals include sold item sale-line snapshots
- seller sheet totals include missing item current seller prices
- registered and not-provided items are excluded from sheets
- item search finds by reference and title
- `in_stock -> missing` succeeds
- `missing -> in_stock` succeeds
- other reconciliation status changes are rejected
- reconciliation pages require the admin gate
- printable output contains one seller-sheet section per active seller
- French routes and labels are present
