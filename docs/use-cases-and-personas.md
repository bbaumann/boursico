# Use Cases And Personas

## Personas

### Sale-Day Volunteer

Profile:

- non-technical
- often elderly
- helping only during the event
- needs a crystal-clear interface and minimal training

Primary goal:

- enter item references and save customer sales reliably

What this persona does not need:

- seller information
- accounting concepts
- inventory administration
- payment method management

### Intake Operator

Profile:

- more comfortable with computer work
- prepares the event data before the sale

Primary goal:

- create sellers and items from seller-provided lists and confirm which preregistered toys were actually delivered

### Reconciliation Admin

Profile:

- more technical or more detail-oriented than sale-day volunteers
- works after the sale is finished
- operational role rather than a named software account

Primary goals:

- move the event between sale phases
- correct a sale line when the wrong reference was sold
- generate seller sheets
- mark items as `missing` when discovered
- regenerate final sheets

Access model in V1:

- sale status administration requires the shared admin password to unlock `/vente/statut`
- phase changes and sale-line corrections are available after that page is unlocked
- `missing` changes require the shared admin password
- this is a lightweight misuse-prevention control, not a full user/role system

### Payout Operator

Profile:

- operational user after the event
- may not be technical

Primary goal:

- use the final seller sheet to write checks and prepare seller pickup bags

## Use Cases

### UC-01 Create Seller

Actor:

- Intake operator

Description:

- Create a seller record for the event.

Expected outcome:

- Seller exists and receives an assigned seller code for reference generation.

### UC-02 Create Item

Actor:

- Intake operator

Description:

- Create one item per physical toy, linked to a seller.

Expected outcome:

- Item has a unique reference, one seller-set printed price, and an initial status consistent with the intake mode.

### UC-02A Record Email Preregistration

Actor:

- Intake operator

Description:

- Create seller items from a preregistration list received by email before toys are physically delivered.

Expected outcome:

- Seller items exist in `registered` status and are ready for later physical confirmation.

### UC-02B Register Seller In Person

Actor:

- Intake operator

Description:

- Register a seller and their toys when they come in person with the toys physically present.

Expected outcome:

- Seller items are created directly in `in_stock` status.

### UC-02C Confirm Preregistered Delivery

Actor:

- Intake operator

Description:

- Review preregistered items line by line when the seller physically brings toys the day before the sale.

Expected outcome:

- Delivered items are marked `in_stock`.
- Undelivered preregistered items are deleted while still `registered`.
- If an item was mistakenly entered as physically present, it may be deleted while still `in_stock`.
- V1 does not use a `not_provided` status.

### UC-03 Search Item During Sale

Actor:

- Sale-day volunteer

Description:

- Type part or all of a reference and choose the intended item from the matching list.

Expected outcome:

- Correct item is selected without needing to know seller context.

### UC-04 Add Item To Cart

Actor:

- Sale-day volunteer

Description:

- Add the selected item to the current customer cart.

Expected outcome:

- Item is shown in the cart with its printed price and contributes to the subtotal, fee, and final total.

### UC-05 Save Sale

Actor:

- Sale-day volunteer

Description:

- Confirm the sale once the customer is done buying.

Expected outcome:

- Sale is persisted with subtotal, checkout fee, and total paid; affected items are no longer in stock; and the screen resets for the next customer.

### UC-05A Change Sale Phase

Actor:

- Reconciliation admin

Description:

- Move the event between `presale`, `sale`, and `post_sale` from `/vente/statut`.
- The admin page must be unlocked with the shared admin password in V1.

Expected outcome:

- The current phase is visible on every page.
- `/` opens the workflow that matches the phase: `/vendeurs`, `/caisse`, or `/rapprochement`.
- The server applies the corresponding edit and checkout rules.

### UC-05B Correct Mistaken Sale Line

Actor:

- Reconciliation admin

Description:

- Find a sold item that was added to a sale by mistake.
- Confirm correction from the already-unlocked `/vente/statut` admin page.

Expected outcome:

- The item returns to `in_stock` and can be sold again.
- The original sale line is marked corrected and remains as historical evidence.
- Seller-sheet payout output ignores the corrected sale line.
- The original sale totals are not recalculated.

### UC-06 Print Receipt

Actor:

- Sale-day volunteer

Description:

- Optionally print a receipt in the rare reimbursement case.
- The operator fills one free-text receipt field only when the customer asks for a reimbursement receipt.

Expected outcome:

- Receipt is produced without complicating the normal checkout flow.

### UC-07 Generate Seller Sheets

Actor:

- Reconciliation admin

Description:

- Generate one printable seller sheet after the sale is over.
- This action is available only when the event phase is `post_sale`.

Expected outcome:

- Each sheet shows returned items first, payable items second, and final amount to pay.
- Returned item rows include printed checkboxes for physical gathering.

### UC-08 Mark Missing Item

Actor:

- Reconciliation admin

Description:

- Change an item from `in_stock` to `missing` when it cannot be found physically during post-sale gathering.
- This action requires the shared admin password in V1.
- This action is available only when the event phase is `post_sale`.

Expected outcome:

- Regenerated seller sheet moves the item from returned to payable with a `MANQUANT` marker.

### UC-09 Prepare Seller Pickup Bag

Actor:

- Payout operator

Description:

- Use the final sheet to prepare the seller's check and physical bag.

Expected outcome:

- Bag contains the final seller sheet, returned items, and check.

## UX Implications From Personas

- The sale-day flow must prioritize speed, readability, and lack of ambiguity.
- The system must avoid exposing unnecessary data during sales.
- Reconciliation can tolerate a more technical workflow because it is used later by different people.
