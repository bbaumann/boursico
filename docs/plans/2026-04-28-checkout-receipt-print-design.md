# Checkout Receipt Print Design

## Goal

Build checkout slice 3: let the operator optionally enter receipt details, save the sale, and print a receipt from the checkout flow after the sale has been persisted.

## Source Of Truth

This slice implements the existing accepted docs:

- `docs/features.md`: V1 receipt handling uses one optional receipt-details field.
- `docs/checkout-contract.md`: `Save and print receipt` is only available when receipt details are filled.
- `docs/decisions.md` D-025: printing must save first and must not create ambiguity about persistence.
- `docs/architecture.md`: the operations UI owns optional receipt printing.

## Scope

This slice includes:

- one optional receipt-details text field on `/caisse`
- storing normalized receipt details on the saved sale
- `Enregistrer et imprimer` in the finalization modal
- disabling that action when receipt details are blank
- a print-only receipt section rendered by the checkout page
- opening the browser print dialog only after sale persistence succeeds
- returning to a fresh checkout page after printing
- keeping the existing conflict behavior for save-time conflicts

This slice does not include:

- a separate receipt route
- a receipt-requested flag
- a receipt-printed flag
- receipt reprinting or sale lookup
- printer configuration beyond the existing `Printing:ReceiptHeaderText`

## Architecture

The checkout page remains the user-facing workflow owner at `/caisse`. Razor renders the receipt details field, modal action, and a hidden print-only receipt container. The page model exposes `Printing:ReceiptHeaderText` so the printed receipt uses the configured association/event header.

The existing `POST /api/ventes` endpoint remains the sale creation API. Its request body gains an optional `receiptDetails` string. `CheckoutService.SaveSaleAsync` accepts that value, trims it, stores `null` when blank, and persists the normalized text on `sale.receipt_details` in the same transaction as the sale lines and item status updates.

The JavaScript keeps the cart in memory. `Enregistrer la vente` saves without printing. `Enregistrer et imprimer` sends the same sale request with receipt details, renders the receipt from the confirmed cart and response totals, calls `window.print()`, and then reloads the page after the print dialog returns.

## UI Behavior

The cart panel adds a French receipt details text area, empty by default. The finalization modal has three actions:

- `Retour`
- `Enregistrer la vente`
- `Enregistrer et imprimer`

`Enregistrer et imprimer` is disabled while receipt details are blank or whitespace. When receipt details become non-blank, the action is enabled. Both save actions still run the authoritative save-time validation through `POST /api/ventes`.

If save-time validation fails, the modal closes, conflicting cart rows are highlighted, the cart stays intact, and no print dialog opens.

## Printed Receipt

The printed receipt contains:

- configured receipt header text
- sale number returned by the API
- sale timestamp from the browser at print time
- receipt details entered by the operator
- cart item lines with reference, title, and price
- subtotal, checkout fee, and total paid

Only the receipt area is visible in print media. The normal checkout UI is hidden for printing.

## Testing

Tests should cover:

- `CheckoutService.SaveSaleAsync` stores trimmed receipt details
- blank or whitespace receipt details are stored as `null`
- `POST /api/ventes` accepts `receiptDetails` and persists it
- `/caisse` renders French receipt details UI, print modal action, and print-only receipt hooks
- JavaScript sends receipt details to `/api/ventes`
- JavaScript calls `window.print()` only in the print save path
- print CSS hides the checkout UI and shows the receipt area
