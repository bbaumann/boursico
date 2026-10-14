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

An item has no reference until its seller's deposit is started at a deposit
desk (`Commencer le dépôt`), or until the row is saved on an already started
deposit; see Item Reference Allocation below. Until then, screens show an em
dash (`—`) in its place: on the seller page, on the `/donnees` Articles tab,
and on the item edit page.

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
post-sale physical sorting by seller. The seller code no longer influences the
item-reference letter, which is decided by the deposit desk (see Item
Reference Allocation below); it remains the seller's identifier.

## Item Reference Allocation

Item references are allocated at the deposit desk, while the toy is in the
operator's hands, so that its label can be stuck straight away. The full
design is in `docs/superpowers/specs/2026-10-13-deposit-desk-letters-design.md`
(decision D-051, which replaces D-050).

### Deposit desks own letters

A deposit desk is one computer at one physical queue, opened on
`/vendeurs/poste`. Opening a desk assigns it `Labels:LettersPerDesk` letters
(default 2): the free, non-exhausted letters with the fewest numbers used, ties
broken by alphabet order. At most one open desk holds a letter at any time
(enforced by the server), so label sheets never travel between desks and the
next number the application gives is always the next label on the sheet in
front of the operator. The desk is identified by an `HttpOnly`,
`SameSite=Strict` cookie `poste-depot` valid for 4 days. A desk is closed by
itself (`Fermer le poste`) or by an admin (`Forcer la fermeture` on
`/administration`); its letters then become free.

### Per-letter counters and labels per letter

Each letter has a `last_number` that only increases: deleting an item never
lowers it, so a number whose label was already peeled is never handed out
again. The number of labels printed per letter is a single setting, default
455 (7 sheets of 65), stored in the database and edited on `/administration`
(`Étiquettes par lettre`, between 1 and 999, never below the highest number
already used). It replaces the old `Labels:SoftCapacityPerLetter`. A letter is
exhausted when its counter reaches that number; exhaustion is computed, so
raising the setting makes exhausted letters usable again.

### When references are allocated

- `Commencer le dépôt` on a seller's page gives every row that has no
  reference the desk's next numbers, in row creation order, in one run, and
  marks the deposit as started.
- Once started, saving a new row gives it the next number in the same save.
- Nothing is allocated on a non-started deposit, nor by the fee step's
  `Continuer`. `Finaliser le dépôt` is refused until the deposit is started.
- Allocation only happens during `presale`.

### Choosing letters for a run

Among the letters the desk holds that are not exhausted:

1. **Seller's letter**: if the seller already has references and the desk
   holds the letter of their most recent one with room for the whole run, use it.
2. **One letter for the whole run**: otherwise the held letter with the most
   room, if it can take the whole run.
3. **Split**: otherwise fill the seller's letter, then the other held letters
   from most room to least, then free letters taken on the spot (least used
   first).
4. **No room**: if that cannot provide the whole run, nothing is allocated and
   the save is refused.

After every allocation, exhausted held letters are released, and the desk takes
free letters (least used first) to get back to `LettersPerDesk` usable letters;
the page tells the operator which sheet to fetch.

Canceling a validated deposit keeps the references and the started marker; the
labels are physically stuck on the toys by then, so releasing the numbers would
be actively wrong. Rows added afterwards get a reference when saved.

## Why This Reference Scheme Was Chosen

- adhesive labels can be printed weeks before the event, when no seller is
  known yet, because the reference does not depend on the seller code
- the operator sticks each label at the moment the toy is checked, which
  requires the number to be known then, not at contract print
- letters are owned by one open desk each, so a letter's sheets sit on one
  desk and screen and sheet never disagree; no numbering buffer is needed
- the letter is a physical sort bucket: after the sale, volunteers sort
  unsold items by letter then number into twenty piles, and a seller's recap
  sheet sends one volunteer to collect that seller's items out of the piles
- twenty letters means twenty volunteers can sort in parallel; assigning the
  least-used free letters to new desks keeps the buckets reasonably balanced
- a pre-registered seller's run is allocated in one click and is not split
  while the desk has a letter that can take it
- late-added items stay next to the seller's run in the same letter when it
  has room
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
