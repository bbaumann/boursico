# Excel Seller And Item Prefill Design

## Goal

Reduce operator data entry when a seller provides an Excel file, without adding a separate import workflow.

Excel parsing is an intake helper. It fills the existing editable seller and item form, then the operator reviews and saves through the normal server-side validation and persistence path.

## Scope

This current V1 slice includes:

- a combined seller and item intake form for creating a seller with item rows in one save
- an Excel upload button on that form
- parsing one `.xlsx` workbook with exactly one visible seller worksheet
- filling seller fields and item rows from the workbook
- appending parsed item rows to an existing seller when the seller already exists
- server-side seller code and item reference generation
- French operator-facing labels, validation messages, and routes

This slice does not include:

- direct persistence during Excel upload
- a staging import table
- an import confirmation workflow
- bulk import of multiple seller sheets from one workbook
- seller self-service
- `.xls` or OpenDocument spreadsheet parsing
- fuzzy duplicate resolution
- storing the uploaded workbook after parsing

## User Workflow

### New Seller From Manual Entry

An operator opens `/vendeurs/creer`.

The page shows seller fields, intake mode, and item rows on one form. The operator fills the seller and item rows manually, then clicks the save button.

The server validates the seller and rows. If the seller does not already exist, the server creates the seller and items in one transaction.

### New Seller From Excel

An operator opens `/vendeurs/creer` and clicks `Importer depuis Excel`.

The browser asks for one `.xlsx` file. The server parses the single worksheet, then returns form values:

- seller name
- phone when present
- postal address set to `UNKNOWN` until address parsing is formally designed
- postal code and city when the workbook `CP VILLE` value exactly matches the application's authoritative Isere city list
- paid seller fee defaulted to `EUR 0`
- item title and seller price rows
- default intake mode `Enregistrement`

The page stays editable. The operator corrects any extracted values, chooses the final intake mode if needed, then clicks the normal save button.

No seller or item is saved by the upload action.

### Existing Seller From Excel

If parsing identifies an existing seller, the operator is redirected to the existing seller page at `/vendeurs/{id}`.

The item rows extracted from Excel are appended to the existing `Ajouter des articles` form. The page shows a visible French alert:

`Ce vendeur existe déjà. Les articles du fichier Excel ont été ajoutés au formulaire ci-dessous. Vérifiez puis enregistrez le lot.`

The redirect does not persist the parsed items. The operator still reviews and clicks `Enregistrer le lot`.

### Existing Seller Detected At Save Time

The create page repeats the duplicate-seller check when the operator saves, because database state may have changed since Excel parsing.

If the seller now exists, the server does not create a second seller. It redirects to `/vendeurs/{id}`, carries the pending item rows to the existing seller page, and shows the same alert.

## Accepted Workbook Format

The first version accepts one `.xlsx` file with exactly one visible worksheet for one seller.
Hidden helper worksheets, such as postal-code lookup sheets embedded in the template, are ignored.

The parser should support the existing historical template shape:

- seller name near the `NOM PRENOM` label
- phone near the `TELEPHONE` label
- postal code and city near the `CP VILLE` label, formatted as `38120 Saint-Égrève`
- item rows in columns `B:C`, with title in column `B` and seller price in column `C`

For seller information, the parser should find labels instead of hard-coding every cell. Product rows use the fixed template item area in columns `B:C`.

The example workbook shows the first product row at row `7`. The old template expected 15 item rows, but row `21` is not a hard limit. In real files, sellers may add more product rows below row `21`, so the parser must continue reading the same `B:C` product block beyond row `21`.

The first product row is `7`. The parser should read rows downward until the first fully blank row after the product block, then stop so footer text below the product area is not treated as an item. Only complete title-and-price rows before that stop point are imported.

## Field Mapping

Seller fields:

- workbook seller name maps to `FirstName` and `LastName` using the template convention `NOM PRENOM`
- phone maps to `Phone`
  - if Excel stores the phone as a number and drops the leading `0`, the parser restores that leading `0`
- postal address maps to the temporary value `UNKNOWN` until workbook address handling is formally designed
- `CP VILLE` maps to `PostalCode` and `City` only when the city exactly matches one of the canonical city values for that postal code in `wwwroot/data/isere-postal-cities.json`
- if `CP VILLE` is present but does not match the authoritative list, the upload fails with a French validation message instead of guessing a closest city
- email remains blank unless the workbook contains an obvious email cell in a future template
- paid seller fee maps to `EUR 0`

Item fields:

- column `B` maps to item title
- column `C` maps to seller price

Intake mode:

- Excel prefill defaults to `Enregistrement`
- the operator may switch to `Dépôt` before saving when the toys are physically present

Statuses on save:

- `Enregistrement` creates items as `registered`
- `Dépôt` creates items as `in_stock`

## Duplicate Seller Detection

The first version uses exact normalized matching, not fuzzy matching.

The duplicate lookup should normalize:

- first name
- last name
- phone when present
- email when present later

Recommended first-pass rule:

- match exact normalized first name and last name
- when phone is available, require phone to match too
- when phone is not available, exact name match is enough

This may miss some real duplicates, but it avoids adding a complex duplicate-resolution workflow.

When a duplicate is found:

- do not update existing seller details automatically
- append pending item rows to that seller's item form
- let the operator edit and save the item rows normally

## Persistence Design

Excel upload never writes seller or item data.

Normal save uses a server-side service method such as:

```csharp
Task<CreateSellerWithItemsResult> CreateSellerWithItemsAsync(
    CreateSellerWithItemsRequest request,
    CancellationToken cancellationToken);
```

For a new seller, the method should:

1. validate the seller fields and item rows
2. check for an existing seller
3. allocate the next seller code
4. create the seller
5. create item rows with references based on the seller code
6. save seller and items in one transaction

For an existing seller append, the method should:

1. load the seller
2. compute the next seller-local item number from current items
3. append item rows
4. save all appended rows in one transaction

If any item row is invalid, save nothing.

## Seller Code And Item References

The client never generates seller codes, item numbers, or references.

For a new seller, the server allocates the seller code before creating item references. Item references use the existing format:

- `SELLERCODEITEMNO`
- example: `BB001`

For existing sellers, appended item numbers continue after the current maximum item number for that seller.

The implementation should treat seller-code generation as concurrency-sensitive. The current count-based code allocation is acceptable for normal use, but this feature should harden the service boundary:

- generate the code inside the transaction
- rely on the unique `seller_code` constraint
- if a concurrent insert creates a conflict, retry with the next available code or return a clear validation failure

## Pending Rows Across Redirect

When an existing seller is detected during parse or save, the parsed item rows need to survive a redirect to `/vendeurs/{id}`.

Use short-lived server-side session state for this handoff rather than query strings or database staging.

The state should contain:

- seller id
- intake mode
- item rows
- alert message key

The existing seller detail page consumes the pending rows once and clears them from session.

## Validation And Errors

Upload-time errors should be shown in French and should not save anything.

Blocking upload errors:

- file is missing
- file is not `.xlsx`
- workbook has no usable worksheet
- workbook has more than one non-empty seller worksheet
- seller name cannot be found
- item header columns cannot be found
- item row has title without price
- item row has price without title
- price cannot be parsed as euros

Save-time errors remain authoritative:

- seller fields are required
- at least one complete item row is required for this intake flow
- title and price must both be filled for each item row
- seller price must parse as euros
- seller price must be greater than or equal to zero

The page should preserve the operator's current form values after validation failures.

## UI Decisions

User-facing UI remains French.

Recommended labels:

- `Importer depuis Excel`
- `Fichier Excel`
- `Importer`
- `Enregistrer le vendeur et les articles`
- `Ce vendeur existe déjà. Les articles du fichier Excel ont été ajoutés au formulaire ci-dessous. Vérifiez puis enregistrez le lot.`

The existing seller page keeps the current `Ajouter des articles` form. It can receive pending rows from an Excel parse or duplicate redirect.

The sellers index can keep one primary action, `Créer un vendeur`, because Excel import starts inside the seller creation form.

## Testing Strategy

Service tests should cover:

- creating a seller with item rows in one transaction
- appended item references continue from the existing seller's max item number
- `Enregistrement` creates `registered` items
- `Dépôt` creates `in_stock` items
- duplicate seller detection redirects instead of creating a second seller
- invalid item rows save nothing

Parser tests should cover:

- valid one-sheet workbook maps seller and rows
- product rows after row `21` are imported
- parsing stops at the first fully blank row after the product block
- missing seller name is rejected
- title without price is rejected
- price without title is rejected
- non-`.xlsx` files are rejected

Page tests should cover:

- `/vendeurs/creer` renders seller fields and item rows on one form
- Excel upload fills the form but does not persist data
- duplicate parse redirects to the existing seller page with pending item rows
- pending item rows are shown once and then cleared
- French labels and validation messages are present

## Documentation Updates For Implementation

When implemented, update:

- `docs/vnext-backlog.md` to mark the feature as designed or implemented
- `docs/features.md` if this becomes part of the active supported workflow
- `docs/domain-model-and-references.md` if duplicate seller detection or address handling changes the domain model
- `docs/decisions.md` with a lightweight ADR for Excel as form prefill, not direct import
