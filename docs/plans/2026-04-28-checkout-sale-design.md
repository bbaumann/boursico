# Checkout Sale Validation And Persistence Design

## Goal

Build checkout slice 2: validate the in-memory cart against the live database, show a final confirmation modal, persist the sale, mark sold items, and handle stale-cart conflicts.

## Scope

This slice includes:

- pre-finalize cart validation
- confirmation modal with totals and item count
- sale persistence
- save-time validation inside the sale transaction
- item status transition from `in_stock` to `sold`
- conflict highlighting and French operator messages
- checkout reset after a successful save

This slice does not include:

- buyer address and print action
- print action
- receipt print view
- sale listing or cancellation

Those behaviors belong to later slices.

## Architecture

The checkout page remains a Razor Page at `/caisse`. Razor owns the page shell, the initial catalog JSON, and the static modal markup.

Checkout JavaScript owns transient cart state and calls narrow resource-oriented API endpoints for interactive server work:

- `POST /api/caisse/validation`
- `POST /api/ventes`

This follows `technologies.md` and D-029: Razor Pages remain the primary UI mechanism, but interactive checkout actions use focused `/api/...` endpoints. The endpoints stay thin and delegate business rules to `CheckoutService`.

## API Contract

Both endpoints accept JSON with a cart item id list:

```json
{
  "itemIds": [1, 2, 3]
}
```

`POST /api/caisse/validation`

- rejects an empty cart
- checks that every requested item exists and is currently `in_stock`
- returns totals if all items are valid
- returns conflicting item ids if any item is unavailable
- does not modify the database

`POST /api/ventes`

- rejects an empty cart
- starts a database transaction
- rechecks every requested item inside the transaction
- rejects conflicts without creating a sale
- creates one `sale`
- creates one `sale_line` per item, snapshotting `seller_price_cents`
- sets every sold item status to `sold`
- stores subtotal, checkout fee, total paid, and UTC sold time

## User Interface

The checkout page adds a French `Finaliser` button and a finalization modal. The modal shows:

- number of items
- subtotal
- checkout fee
- total

The modal has exactly two actions in this slice:

- `Retour`
- `Enregistrer la vente`

`Enregistrer et imprimer` remains absent until slice 3.

## Conflict Handling

If validation or save detects unavailable items:

- the modal stays closed or closes
- no sale is persisted
- conflicting cart rows are highlighted prominently
- a strong French message tells the operator that an item was already sold or is no longer available
- the rest of the cart remains intact
- the operator must remove the conflicting rows manually before retrying

## Success Handling

After a successful save:

- the cart is cleared
- the operator sees a short French success message
- the page reloads so the in-memory catalog is refreshed from the live database

## Testing

Tests should cover:

- validation succeeds for available items and returns correct totals
- validation rejects empty carts and unavailable items
- saving creates `sale` and `sale_line` rows with correct totals and snapshots
- saving changes sold items to `sold`
- save-time conflict rejects without partial persistence
- API endpoints return success and conflict payloads
- `/caisse` exposes French finalization controls and modal hooks
- JavaScript contains calls to the two API endpoints and conflict-row behavior
