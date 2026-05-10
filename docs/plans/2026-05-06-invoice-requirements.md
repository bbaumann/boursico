# Invoice Requirements

## Goal

Replace the current optional receipt output with a legally usable invoice output for sales where the customer asks for a printed document.

The operator-facing wording must use `Facture`. New internal code concepts should use `Invoice`. Existing historical `receipt` names may be migrated during implementation, but no new user-facing receipt wording should be added.

## Source Feedback

User feedback identified mandatory invoice content:

- invoice issue date
- unique chronological invoice number
- seller identity
- buyer identity and address
- item quantity, designation, unit price excluding VAT, and VAT handling
- totals excluding VAT and including VAT
- Association Familiale Saint-Egreve logo at the top of the invoice

## Scope

This specification covers the printed invoice generated from checkout.

It includes:

- invoice terminology
- invoice numbering
- invoice issue date
- seller and buyer identity
- VAT exemption handling
- invoice line and total content
- AFSE logo placement
- test expectations

It does not include:

- a separate billing address
- VAT collection or VAT rate selection
- accounting exports
- electronic signature or archive certification
- retroactive re-numbering of historical receipt output

## Terminology

Operator-facing UI, page titles, buttons, helper text, validation messages, printed headings, and user-facing routes must use French wording.

Required wording changes:

- `Reçu` becomes `Facture`
- `reçu` becomes `facture`
- `Enregistrer et imprimer` can remain if it still describes the action clearly, but any surrounding document wording must say `facture`

New internal code should use `Invoice`, for example `InvoiceNumber`, `InvoiceIssuedAt`, or `InvoiceNumberGenerator`.

If implementation keeps old `receipt` identifiers temporarily, that is only a migration detail. The target model is invoice terminology.

## Invoice Number

Every generated invoice must have one unique invoice number.

Format:

```text
YYMMAFBJNNNN
```

Example:

```text
2611AFBJ0001
```

Meaning:

- `26`: year `2026`
- `11`: month `11`
- `AFBJ`: fixed code for `Association Familiale - Bourse Jouets`
- `0001`: four-digit chronological sequence number

Numbering rules:

- invoice numbers are assigned server-side, not in browser JavaScript
- the sequence is chronological and continuous for the invoice numbering period
- the first invoice for the `2611AFBJ` period is `2611AFBJ0001`
- the next invoice is `2611AFBJ0002`
- a failed sale or failed invoice creation must not consume a number
- a persisted invoice number must never be reused
- sale corrections or refunds must not rewrite the original invoice number

Implementation must define the source of the `YYMM` period explicitly. The expected default is the configured bourse/event month, for example November 2026 gives `2611`, rather than the workstation clock at print time.

## Issue Date

Every invoice must show the date on which the invoice is issued.

Rules:

- the issue date is persisted by the server when the invoice is created
- the printed invoice uses the persisted issue date
- the browser clock must not be the source of truth for the legal issue date
- display formatting must be French

## Seller Identity

Every invoice must show the seller identity block:

```text
Association Familiale
33 Av. de l'Europe, 38120 Saint-Égrève
SIRET : 306 041 997 00058
Association loi 1901
```

This is the invoicing seller identity for the document. It is not the individual item seller from the bourse.

## Buyer Identity

Every invoice must show the customer identity.

Required buyer content:

- customer full name or organization
- customer address
- customer postal code and city

There is no separate billing address. The invoice always uses the customer address.

Implementation should reuse the checkout customer data rather than creating a second billing-address concept. If the current checkout address field is retained, it should be treated as the customer address used for invoice printing.

The printed customer block should be a single block, not separate buyer and address sections. It should appear beside the seller identity block to keep the printed invoice compact:

```text
NOM Prenom
Adresse
CP Ville
```

## Invoice Header And Logo

The invoice header must not print the application event header or navigation.

The invoice title must be the invoice word and number, for example:

```text
Facture 2611AFBJ0001
```

The Association Familiale Saint-Egreve logo must appear at the top of the invoice. The canonical logo asset is:

```text
https://www.af-st-egreve.org/style/logo-afse.jpg
```

The logo asset should be stored under the web application's static assets and rendered in the print-only invoice header.

## Invoice Lines

For each sold item, the invoice must show:

- quantity
- precise item designation
- unit price excluding VAT
- line total excluding VAT

Current item sales are one physical item per sale line, so quantity is expected to be `1` per line unless a future model introduces grouped quantities.

The item designation should use the item title and may include the item reference when useful for traceability. The designation column should receive the largest width in the printed table. The unit price header should be compact, using `P.U. HT`.

## VAT

Association Familiale is treated as VAT-exempt for this application.

All invoice prices are shown excluding VAT.

The VAT exemption text must appear once per invoice:

```text
TVA non applicable en vertu de l'article 293 B du CGI.
```

The exemption text must not be repeated on every item line.

There is no VAT rate selector, VAT amount entry, or per-line VAT rate in scope.

## Totals

The invoice must show the required totals clearly.

Required total content:

- item subtotal excluding VAT
- association checkout fee excluding VAT, if the sale includes one
- total excluding VAT
- VAT amount, shown as `0,00 EUR`
- total including VAT

Because VAT is not applicable, the total including VAT equals the total excluding VAT.

Existing checkout pricing remains:

- item subtotal = sum of sold item prices
- association checkout fee = 10 percent of item subtotal, rounded by the current checkout rule
- total paid = item subtotal + association checkout fee

The invoice should make the association fee visible as a separate total line or invoice line using French wording consistent with the checkout UI, for example `Frais association familiale`.

## Checkout Behavior

The invoice is generated only when the operator chooses the print path.

Expected behavior:

- sale save without printing does not create an invoice number
- sale save with printing creates and persists invoice data in the same successful transaction as the sale
- if sale validation fails, no invoice number is assigned
- after a successful invoice save, the browser can open the print dialog
- an already saved sale may print an invoice from the read-only sale detail only if the customer address was captured on the original sale
- read-only sale detail printing must reuse an existing persisted invoice number and issue date
- if an addressed saved sale has no invoice number yet, the read-only print action may allocate the next invoice number once and persist its issue date before printing
- if the saved sale has no customer address, the read-only detail must not offer invoice printing

Reprint support must reuse the persisted invoice number and issue date. It must not create a new invoice number for the same invoice.

## Configuration

The following values should be configurable or centralized rather than duplicated in Razor and JavaScript:

- invoice prefix event period, such as `2611`
- fixed invoice code `AFBJ`
- seller name
- seller address
- seller SIRET
- seller legal status
- VAT exemption text
- logo asset path

The configuration should still validate at startup so a missing required invoice setting is found before sale day.

## Testing Expectations

Implementation should add tests for:

- invoice number format `YYMMAFBJNNNN`
- server-side chronological sequence allocation
- no number consumed when sale or invoice creation fails
- invoice issue date persisted server-side
- rendered invoice heading uses `Facture <invoice number>`
- rendered invoice includes the AFSE logo
- rendered invoice includes the seller identity block
- rendered invoice includes buyer name, customer address, postal code, and city in a single customer block beside the seller identity block
- rendered invoice line table includes quantity, designation, `P.U. HT`, and line total excluding VAT
- rendered invoice table gives the designation column the largest width
- VAT exemption text appears exactly once
- totals show excluding VAT, VAT amount `0,00 EUR`, and including VAT
- old operator-facing `Reçu` wording is removed from invoice-related UI

## Open Implementation Notes

- The `YYMM` period should probably come from event configuration, not the system date. Confirm this before implementation if no event-month setting already exists.
- Existing docs and code still use receipt terminology in several places. Implementation should plan a controlled rename rather than mixing `receipt` and `invoice` in new work.
- The AFSE logo asset is `https://www.af-st-egreve.org/style/logo-afse.jpg`.
