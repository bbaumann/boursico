# Checkout Catalog UI Design

## Goal

Build the first checkout slice: a French `/caisse` page that loads an in-memory catalog, live-filters a table, and lets an operator add items to a local cart without validating or saving a sale.

## Scope

This slice includes:

- a sale-day checkout route at `/caisse`
- a read-only catalog of items currently `in_stock`
- client-side filtering against `reference + title`
- click-to-add cart behavior
- duplicate-add feedback
- local cart removal
- local subtotal, checkout fee, and total display

This slice does not include:

- cart validation against the live database
- sale persistence
- final confirmation
- conflict handling
- optional receipt printing

Those behaviors belong to later checkout slices.

## Architecture

The page stays within the existing Razor Pages application. `CheckoutService` exposes a lightweight catalog query over `ItemEntity` records with status `in_stock`. The Razor page serializes that catalog into the page once, and `site.js` owns the local checkout interaction for filtering, cart mutation, duplicate messages, and total calculation.

No API endpoint is required for this slice. That keeps the first slice close to the documented checkout contract while avoiding save-time concerns that belong to slice 2.

## User Interface

The page route and every operator-facing label are French.

The page contains:

- a search field labeled for checkout lookup
- a results table with reference, title, and printed price
- a cart area with selected lines and remove actions
- subtotal, checkout fee, and final total
- an empty-cart state
- duplicate-add feedback in French

Seller information must not appear in the checkout page.

The finalization action is omitted in this slice so the UI does not imply that sale saving exists.

## Data Flow

On GET `/caisse`, the page model asks `CheckoutService` for the catalog. The query returns only items with status `in_stock`, ordered by reference, with the fields needed by the client:

- item id
- reference
- title
- seller price in cents

The client stores the catalog in page memory only. Filtering and cart behavior use only that in-memory data until the page is reloaded.

## Error Handling

This slice handles only local UI errors:

- adding the same item twice shows a clear French message
- removing an item clears any duplicate message for that item
- an empty filter result shows a French empty state

Database availability and stale item conflicts are handled by later slices.

## Testing

Tests should cover:

- `CheckoutService` returns only `in_stock` catalog entries
- the `/caisse` route loads successfully
- the page exposes French checkout labels
- the page includes the serialized catalog and JavaScript hooks
- the page does not expose seller names or seller contact information
- the page has no save, confirmation, or print action yet
