# Domain Model And References

## Core Domain Model

The project uses a small item-centric operational model with seller-aware reconciliation.

Operationally, one database file represents one event in V1. Event metadata is stored in application configuration rather than as a relational entity.

## Entities

### Seller

Represents the person or organization that brings toys for sale.

Suggested fields:

- seller id
- first name
- last name
- seller code
- contact information as needed

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
- optional `receipt_details` text

### SaleLine

Represents one sold item inside a sale.

Suggested fields:

- sale line id
- sale id
- item id
- seller price at time of sale

## Item Status Model

V1 uses one simple status field:

- `registered`
- `in_stock`
- `not_provided`
- `sold`
- `missing`

Derived meanings:

- payable items = `sold` + `missing`
- returned items = `in_stock`
- preregistered but excluded from sale/reconciliation = `registered` + `not_provided`

Lifecycle meaning:

- `registered`: preregistered in the system but not yet physically handed over
- `in_stock`: physically present in association stock and eligible for sale or return
- `not_provided`: preregistered but never actually handed over physically
- `sold`: sold during the event
- `missing`: expected to be returned after the event, but not found physically

## Price Model

Each item stores one price:

- seller price: the amount payable to the seller if the item is payable and the printed per-item price seen by the customer in V1

Each sale stores the transaction-level customer payment values:

- subtotal seller price: the sum of the item prices in the cart
- checkout fee: 10 percent of the subtotal, rounded to the nearest cent with midpoint values rounded away from zero
- total paid: subtotal + checkout fee

V1 seller settlement uses seller price only.

Persistence rule:

- money values are stored as integer cents in the database

## Receipt Model

V1 uses one optional receipt text field:

- `receipt_details`

Meaning:

- if `receipt_details` is null or empty, no receipt was requested
- if `receipt_details` is filled, a receipt was requested for later reimbursement handling

The field may contain the customer name, organization, address, or any other text the operator needs to print on the receipt.

V1 does not track a separate:

- receipt requested flag
- receipt printed flag

## Reference Scheme

## Final Operator-Facing Format

References use this format:

- `SELLERCODE-ITEMNO`

Example:

- `BK-014`

## Seller Code

Seller codes are two-letter codes assigned once per seller for the event.

### Safe Alphabet

Use uppercase letters excluding:

- `I`
- `O`
- `Q`
- `V`

Safe alphabet:

- `A B C D E F G H J K L M N P R S T U W X Y Z`

This gives 22 letters and 484 possible two-letter seller codes.

### Seller Code Assignment Order

Assign seller codes in a spread order by first letter first.

Example prefix sequence:

- `AA`
- `BA`
- `CA`
- `DA`
- ...
- `ZA`
- `AB`
- `BB`
- `CB`

This is done to spread sellers across first letters for post-sale physical sorting.

## Item Number Within Seller

Each seller gets a fixed-width per-seller sequence:

- `001`
- `002`
- `003`

Later-added items simply continue the sequence.

Example:

- `BK-001`
- `BK-002`
- `BK-003`

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

Missing items are shown in the payable list with a visible `MISSING` marker.

### Returned List

Contains:

- items with status `in_stock`

### Final Amount To Pay

Computed as:

- sum of seller prices for all items in the payable list
