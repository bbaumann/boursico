# Process And Failover

## Normal Process

### Before The Event

1. Some sellers send their item lists outside the system, typically by email.
2. Intake operator creates sellers in the system.
3. Items are entered as `registered`, whether they come from an emailed list, external form import, or in-person operator entry.
4. When the seller physically delivers toys, the operator completes missing seller contact details, reviews the seller record, removes or corrects incomplete items, prints the deposit contract, and gets it signed.
5. The operator validates the seller deposit from the seller detail page. This sets `seller.deposit_status = deposit_validated` and moves that seller's `registered` items to `in_stock`.
6. If a defect is spotted before the seller signs the deposit contract, the item is not accepted and any corresponding `registered` row may be deleted.
7. References are generated for each item.
8. `in_stock` items from validated seller deposits are prepared for the event.

### During The Sale

The system starts in `presale`.

Admins may leave the system in `presale` while selling if organizers are still doing final item corrections. Sales are still allowed in `presale`.

When organizers want to freeze seller and item master data, an admin unlocks `/administration` with the shared admin password and moves the event to `sale`. This makes `/` open `/caisse` and prevents normal seller and item edits.

During checkout:

- seller and item master data are frozen for the event
- only allowed status changes remain possible after this point

1. Sale-day volunteer fills the required buyer name or organization, postal code, and city.
2. Sale-day volunteer types part or all of a reference.
3. The visible item table is filtered on each keystroke using `reference + title` matching from the in-memory checkout catalog.
4. Volunteer selects the correct item from the filtered table.
5. Item is added to the cart.
6. When the customer is done, volunteer clicks `Finaliser`.
7. The cart is validated against the live database before the finalization modal is shown.
8. If validation passes, the finalization modal shows the item count, total amount, payment method choice, and the actions `Retour`, `Enregistrer la vente`, and `Enregistrer et imprimer`.
9. If the volunteer chooses `Chèque`, cheque number and bank name are required.
10. If the volunteer chooses `Enregistrer la vente` or `Enregistrer et imprimer`, the server re-validates that all cart items are still `in_stock` and belong to validated seller deposits inside the save transaction.
11. The server rejects the save if the event is already in `post_sale`.
12. If validation passes, the sale, buyer details, payment method, and cheque details when relevant are saved and those items become `sold`.
13. If any item is no longer saleable, the save is rejected, the conflicting item is highlighted prominently in red, and the operator must remove it and retry.
14. After a successful save, checkout resets to a fresh state and reloads the in-memory catalog.

### Mistaken Sale Correction

If a volunteer saved a sale with the wrong reference and the real item later needs to be sold, an admin corrects the mistaken sale line from the unlocked `/administration` page.

The correction keeps the original sale stored, marks the sale line corrected, returns the item to `in_stock`, and lets the item be sold again. It does not recalculate the original sale total because the cash/receipt consequence happened at checkout.

If a buyer returns a defective sold item, an admin uses `Rembourser` from `Correction des articles` on the same unlocked `/administration` page. The app shows the buyer refund amount before confirmation: item price plus the refunded 10 percent association fee. The original sale totals stay unchanged, the refund is recorded on the sale line, the item becomes `defective`, and the item is returned to the seller without seller payout. If the refund was entered by mistake, the admin can cancel it from the same page; the app clears the refund fields and restores the item to `sold`.

### After The Sale

1. Admin unlocks `/administration` with the shared admin password and moves the event to `post_sale`.
2. New sale validation and sale save are rejected server-side.
3. Admin opens the reconciliation dashboard at `/rapprochement` after entering the shared admin password.
4. Admin prints all seller sheets from `/rapprochement/feuilles`; sheets are generated only for sellers with at least one `sold`, `missing`, `in_stock`, or `defective` item.
5. An operator physically gathers all returned items from the first sheet section, ticking the printed checkboxes as items are found.
6. If a returned item cannot be found, admin marks it as `missing`.
7. Seller sheet is regenerated from the dashboard.
8. If the `missing` status change was a mistake and the item is found before finalization, admin restores the item to `in_stock` as a reconciliation correction and regenerates the sheet.
9. Defective items appear in the returned-items list with `DÉFECTUEUX`; they are not paid to the seller.
10. Another operator writes the check based on the final amount to pay after the 15 percent association margin and any unpaid seller fee.
11. Check, final seller sheet, and returned items are physically grouped for seller pickup.

## Seller Deposit Validation Workflow

1. Seller item data is recorded from email, external form import, or in-person entry.
2. Preregistered items exist in `registered` status.
3. When toys are physically delivered, the operator reviews the seller item list before signature.
4. The operator completes phone, postal address, postal code, and city before validation.
5. The seller detail page offers `Finaliser le dépôt`.
6. The operator verifies the expected seller fee, paid fee, and remaining fee before printing. If the seller pays at deposit time, the operator updates `Frais payés`.
7. The operator prints `Imprimer le contrat`.
8. The printed contract includes the association name, address, SIRET, seller fee rule, paid fee status, legal mentions, item prices, and only the seller signature area.
9. The signed paper contract is the proof of physical deposit and price agreement; the app does not store a contract number or snapshot.
10. The operator clicks `Valider le dépôt` and confirms `Le contrat est signé`.
11. Accepted `registered` items become `in_stock` together, and normal seller/item edits are locked for that seller.
12. Items that were not physically brought may remain `registered` if the seller might come back later; otherwise the operator may delete them before validation.
13. If validation was confirmed by mistake, an admin may cancel it from the seller page during `presale` by entering the shared admin password.
14. Cancellation moves only items still `in_stock` back to `registered`; `sold`, `missing`, and `defective` items stay unchanged and read-only.
15. Revalidating the deposit tolerates those historical item states and moves only current `registered` items back to `in_stock`.
16. V1 does not use a `not_provided` status.

Remaining seller fees do not block deposit validation. Unpaid fees stay visible and are deducted from seller reconciliation.

## Seller Sheet Workflow

### Default Case

If no item has status `missing`:

- the first generated seller sheet is already the final seller sheet

### Exceptional Case

If an item listed as returned cannot be found physically:

1. Admin enters the shared admin password and updates the item status from `in_stock` to `missing`.
2. The seller sheet is regenerated.
3. The item moves from the returned list to the payable list and is marked `MANQUANT`.

### Correction Case

If an item was marked `missing` by mistake and is found before the seller packet is finalized:

1. Admin enters the shared admin password and restores the item to `in_stock`.
2. The seller sheet is regenerated.
3. The item moves back from the payable list to the returned list.

## Defective Item Workflow

### Defect Found After Acceptance

If an item is already accepted into stock and an admin later spots a defect:

1. Admin opens `/administration`.
2. Admin finds the `in_stock` item under `Correction des articles`.
3. Admin clicks `Marquer défectueux`.
4. The item disappears from checkout and appears on the seller sheet as an item to return.

If the defect marker was a mistake and no buyer refund exists, admin can use `Remettre en stock` from the same page.

### Defect Returned By A Buyer

If a buyer returns a defective sold item:

1. Admin opens `/administration`.
2. Admin finds the sold item under `Correction des articles`.
3. Admin clicks `Rembourser`.
4. The confirmation pop-in shows item price, refunded association fee, and total refund.
5. Admin confirms the refund after reimbursing the buyer operationally.
6. The item becomes `defective`, is returned to the seller, and is excluded from seller payout.

If this refund was created by mistake, admin can click `Annuler le remboursement` from the same `Correction des articles` table. The item returns to `sold`, the sale line refund fields are cleared, and the sale can again count as payable.

## Operational Failures

## Failure Philosophy For V1

V1 intentionally prefers:

- clear failure
- manual fallback
- later reconciliation

over:

- partial hidden offline behavior
- local replay complexity
- distributed conflict handling

## Official Digital Failure Mode

If the central server is unavailable:

- digital sales stop
- operators switch to paper bills
- reconciliation is performed manually later

There is no local per-station replay in V1.

## Paper Fallback Procedure

### Prepared Materials

- pre-numbered paper bill forms
- pens and clipboards
- written fallback instructions

### Procedure

1. Note the outage time.
2. Stop using the digital system.
3. Switch all sale stations to numbered paper bills.
4. Keep completed paper bills together in order.
5. Continue the event manually.
6. After the event, reconcile paper sales against remaining physical stock.

## Hard-Crash Recovery Principle

If the server is physically lost and digital recovery is not practical in time for the event:

- use paper fallback for the rest of the event
- use the physical stock at the end of the event as the authoritative basis for manual reconciliation

Operationally, anything not present in physical stock and not returned can be treated as sold or payable according to the fallback reconciliation rules used by the organizers.

## Reliability Measures Worth Keeping Even In V1

- automatic periodic database backups
- backup before sale start
- backup after sale close
- exportable sales and item lists if practical
- written operator fallback instructions

## Sensitive Action Control

- every sale must be persisted immediately
- item selection in a cart must not create a hidden reservation state
- sale save must reject new sales in `post_sale`
- sale save must re-check current item availability before persisting
- item availability must prevent double sale after confirmation
- mistaken sale correction must mark the sale line corrected instead of deleting or rewriting the original sale
- seller sheets must always be reproducible from current database facts
- marking an item as `missing` must require the shared admin password
- restoring a mistakenly marked `missing` item to `in_stock` must require the shared admin password
- the reconciliation dashboard at `/rapprochement` must be admin-gated
- seller-sheet printing and missing-item reconciliation corrections must require the event phase to be `post_sale`
- all seller sheets must be printed from `/rapprochement/feuilles`
- `Admin:SharedPassword` must be configured for production; blank values and `change-me` are invalid
- V1 does not implement per-user identity or a full authorization model
- the password gate exists to reduce misuse by non-admin operators, not to provide strong security
