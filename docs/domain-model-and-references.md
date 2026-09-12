# Domain Model And References

## Core Domain Model

The project uses a small item-centric operational model with seller-aware reconciliation.

Operationally, one database file represents one event. Event metadata is stored in application configuration rather than as a relational entity. V1 is feature-complete but not live yet.

## Entities

### Seller

Represents the person or organization that brings toys for sale.

Suggested fields:

- seller id
- first name
- last name
- seller code
- optional email
- required phone number
- required postal address
- required postal code
- required city
- identity document nature
- identity document number
- identity document issue date
- identity document issuing authority
- deposit status

### Item

Represents one physical toy.

Suggested fields:

- item id
- seller id
- reference
- optional barcode value if later needed
- title
- seller price
- status

### Sale

Represents one customer checkout event.

Suggested fields:

- sale id
- timestamp
- configured caisse id
- subtotal seller price
- checkout fee
- total paid
- required buyer name or organization
- required buyer postal code
- required buyer city
- optional `buyer_address` text

### SaleLine

Represents one sold item inside a sale.

Suggested fields:

- sale line id
- sale id
- item id
- seller price at time of sale
- corrected at timestamp, when an admin corrected a mistaken sale line
- refunded at timestamp, when an admin refunded a defective sold item
- refund item price, refunded checkout fee, total refunded amount, refund reason, and refund caisse id

If a sale line is corrected, the original sale and sale totals remain stored. The corrected line no longer makes the item unavailable for sale and is ignored by seller-sheet payable output.

If a sale line is refunded because the sold item is defective, the original sale and sale totals remain stored. The refunded line is ignored by seller-sheet payable output, records the configured caisse that paid the refund, and the item becomes `defective` so it is returned to the seller.

### CashRegister

Represents one physical caisse configured by an admin before sale day.

Suggested fields:

- caisse id
- name
- initial cash amount (`fond de caisse`)
- active flag
- display order

## Item Status Model

The current item model uses one simple status field:

- `registered`
- `in_stock`
- `sold`
- `missing`
- `defective`

Seller deposit validation is represented separately on the seller:

- `deposit_pending`
- `deposit_validated`

Derived meanings:

- payable items = active non-refunded `sold` lines + `missing`
- returned items = `in_stock` + `defective`
- preregistered but excluded from sale/reconciliation = `registered`

Lifecycle meaning:

- `registered`: entered in the system but not yet accepted through a signed seller deposit; it may remain pending if the seller might still bring the item later
- `in_stock`: physically present in association stock after seller deposit validation and eligible for sale or return
- `sold`: sold during the event
- `missing`: expected to be returned after the event, but not found physically
- `defective`: physically accepted but not suitable for sale; returned to the seller and not payable

V1 does not use a `not_provided` status. Preregistered items that are not physically delivered may remain `registered`; they are deleted only when entered by mistake or when the seller record should be cleaned up before deposit validation. Validating the seller's signed paper deposit requires complete seller contact and identity document fields, moves that seller's accepted `registered` items to `in_stock`, and locks normal seller/item edits. The signed paper contract is the legal proof; `seller.deposit_status` is only the operational gate.

## Sale Phase

One event-level phase controls normal operations:

- `presale`: preparation mode; sales are allowed and unsold item master data is editable
- `sale`: catalog-lock mode; sales are allowed and item master data is read-only
- `post_sale`: reconciliation mode; new sales are blocked and item master data is read-only

## Price Model

Each item stores one price:

- seller price: the amount payable to the seller if the item is payable and the printed per-item price seen by the customer

Each sale stores the transaction-level customer payment values:

- subtotal seller price: the sum of the item prices in the cart
- checkout fee: 10 percent of the subtotal, rounded to the nearest cent with midpoint values rounded away from zero
- total paid: subtotal + checkout fee

Seller settlement starts from seller price snapshots for payable items, then deducts the 15 percent sale fee and any remaining seller fee. A defective-item buyer refund reimburses the buyer the item price plus the 10 percent checkout fee for that item; it does not change the original sale totals.

Financial closing groups collected money by `sale.cash_register_id` and refunds by `sale_line.refund_cash_register_id`. Per-caisse expected cash starts from the configured `fond de caisse`, adds cash sales for that caisse, and subtracts refunds physically paid by that caisse.

Persistence rule:

- money values are stored as integer cents in the database
- seller prices entered by operators are whole euros in V1, so newly created seller prices are stored as cent values divisible by `100`
- the cents-based column remains intentional to preserve future cents support without changing the persisted model

## Buyer Model

A sale stores required structured buyer details:

- `buyer_name`
- `buyer_postal_code`
- `buyer_city`

These fields identify the person or structure associated with the checkout. They are stored separately from the optional address so sale history can search and display the buyer without parsing `buyer_address`.

The checkout UI offers local city suggestions only for Isère postal codes (`38xxx`) and prefills the city when exactly one known city matches the postal code. Other French departments use free-text city entry.

## Invoice Model

V1 uses one optional customer address text field for invoice printing:

- `buyer_address`
- `invoice_number`
- `invoice_issued_at`

Meaning:

- if `invoice_number` is null, no invoice was created for the sale
- if `invoice_number` is filled, the sale has a persisted invoice number and issue date
- `buyer_address` is the customer address printed on the invoice

The operator-facing field is labeled `Adresse`. It contains the customer address printed on the invoice.

V1 does not track a separate:

- invoice requested flag
- invoice printed flag

## Reference Scheme

## Final Operator-Facing Format

References use this format:

- `<LETTER><NNN>`

Example:

- `B157`

`LETTER` is one character from the safe alphabet below. `NNN` is the number
within that letter, zero-padded to exactly three digits. The padding is
load-bearing: `ORDER BY reference` must match physical label order, so `B009`
must sort before `B010`.

The reference carries no seller information. The letter is a physical sort
bucket for post-sale recollection, not a seller identifier: an operator can
always look up the seller in the administration screens. Identifying the
seller from a stray label is explicitly not a requirement.

An item has no reference until its seller's deposit reaches the
contract-print step; see Item Reference Allocation below. Until then, screens
show an em dash (`—`) in its place: on the seller page, on the `/donnees`
Articles tab, and on the item edit page.

## Seller Code

Seller codes are two-letter codes assigned once per seller for the event.

### Safe Alphabet

Use uppercase letters excluding:

- `A`
- `F`
- `I`
- `O`
- `Q`
- `V`

Safe alphabet:

- `B C D E G H J K L M N P R S T U W X Y Z`

This gives 20 letters and 400 possible two-letter seller codes. Item
references reuse this same 20-letter alphabet for their single leading
letter.

### Seller Code Assignment Order

Assign seller codes in a spread order by first letter first.

Example prefix sequence:

- `BB`
- `CB`
- `DB`
- `EB`
- ...
- `ZB`
- `BC`
- `CC`
- `DC`

This spreads sellers across first letters. It was originally chosen for
post-sale physical sorting by seller, and it now does double duty: because a
seller's own item-reference letter defaults to the first character of their
seller code, this cycling keeps the twenty item-reference letter buckets
balanced too, with roughly ten sellers behind each letter for a typical
100-200 seller event.

## Item Reference Allocation

Item references are allocated when a seller's deposit reaches the
contract-print step (the `Continuer` action on the fee step), not when the
item row is created. This is what lets the association print all adhesive
label sheets weeks before the event, at a time when no seller is yet known.
By the time allocation runs, the seller's item list is final, so each
seller's run of numbers is assigned in one contiguous pass; there is no need
for any numbering buffer.

Choosing the letter for a seller's items follows a four-tier rule:

1. **Top-up**: if the seller already has referenced items, reuse their most
   recently allocated letter if it still has room.
2. **Own letter**: otherwise use the seller's own letter (the first
   character of their seller code) if it has room.
3. **Spill**: otherwise use the least-loaded letter that still has room, so
   the twenty sort piles stay even.
4. **Relax**: otherwise (every letter full) use the seller's own letter
   anyway, past its soft capacity.

Soft capacity per letter is 455, exposed as `Labels:SoftCapacityPerLetter` in
`appsettings.json`. It corresponds to the 7 adhesive label sheets of 65
labels the association prints per letter before the event; the application
itself never reasons about sheets, only about that number.

Canceling a validated deposit keeps any references already allocated to that
seller's items; the labels are physically stuck on the toys by then, so
releasing the numbers would be actively wrong. Revalidating a deposit
allocates references only for items that still lack one.

## Why This Reference Scheme Was Chosen

- adhesive labels can be printed weeks before the event, when no seller is
  known yet, because the reference no longer depends on the seller code
- the letter is a physical sort bucket: after the sale, volunteers sort
  unsold items by letter then number into twenty piles, and a seller's recap
  sheet sends one volunteer to collect that seller's items out of the piles
- twenty letters means twenty volunteers can sort in parallel, so letters
  must stay reasonably balanced, which the seller-code spread order already
  provides
- allocating once the seller's item list is final removes the need for any
  numbering buffer or reserved-number gaps
- late-added items are still trivial to assign: they simply top up the
  seller's existing letter
- references remain readable and hand-friendly

## Sheet Semantics Derived From The Model

### Payable List

Contains:

- items with status `sold`
- items with status `missing`

Missing items are shown in the payable list with a visible `MANQUANT` marker.

Refunded sold lines are excluded from payable output.

### Returned List

Contains:

- items with status `in_stock`
- items with status `defective`

Defective items are shown in the returned list with a visible `DÉFECTUEUX` marker.

### Final Amount To Pay

Computed as:

- item subtotal = sum of seller prices for all items in the payable list
- expected seller fee = EUR 2 per started block of 15 seller items
- remaining seller fee = max(expected seller fee - paid seller fee, 0)
- final payout = max(item subtotal - remaining seller fee, 0)
