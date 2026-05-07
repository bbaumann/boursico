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
- refund item price, refunded checkout fee, total refunded amount, and refund reason

If a sale line is corrected, the original sale and sale totals remain stored. The corrected line no longer makes the item unavailable for sale and is ignored by seller-sheet payable output.

If a sale line is refunded because the sold item is defective, the original sale and sale totals remain stored. The refunded line is ignored by seller-sheet payable output, and the item becomes `defective` so it is returned to the seller.

## Item Status Model

The current model uses one simple status field:

- `registered`
- `in_stock`
- `sold`
- `missing`
- `defective`

Derived meanings:

- payable items = active non-refunded `sold` lines + `missing`
- returned items = `in_stock` + `defective`
- preregistered but excluded from sale/reconciliation = `registered`

Lifecycle meaning:

- `registered`: preregistered in the system but not yet physically handed over; it may remain pending if the seller might still bring the item later
- `in_stock`: physically present in association stock and eligible for sale or return
- `sold`: sold during the event
- `missing`: expected to be returned after the event, but not found physically
- `defective`: physically accepted but not suitable for sale; returned to the seller and not payable

V1 does not use a `not_provided` status. Preregistered items that are not physically delivered may remain `registered`; they are deleted only when entered by mistake or when the seller record should be cleaned up. Items mistakenly entered as present may be deleted while still `in_stock`.

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

Seller settlement uses seller price only. A defective-item buyer refund reimburses the buyer the item price plus the 10 percent checkout fee for that item; it does not change the original sale totals.

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

- `SELLERCODEITEMNO`

Example:

- `BK014`

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

This gives 20 letters and 400 possible two-letter seller codes.

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

This is done to spread sellers across first letters for post-sale physical sorting.

## Item Number Within Seller

Each seller gets a fixed-width per-seller sequence:

- `001`
- `002`
- `003`

Later-added items simply continue the sequence.

Example:

- `BK001`
- `BK002`
- `BK003`

## Why This Reference Scheme Was Chosen

- all items from a seller sort together naturally
- post-sale triage becomes easier
- no need for old reserved global-number gaps
- late-added items are trivial to assign
- references remain readable and hand-friendly
- references do not depend on seller names or hash logic

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
