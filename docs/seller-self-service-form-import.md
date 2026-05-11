# Seller Self-Service Form Import

## Purpose

This document captures the implemented seller self-service intake shape through an external online form provider.

The application must remain private on the local network. The online form provider is only a seller-facing collection surface. Data enters the local application through a file export/import workflow controlled by organizers.

## Recommended Shape

Use a provider-neutral file import:

1. Sellers submit an online form.
2. Organizer exports responses from the provider as CSV, TSV, or a spreadsheet format.
3. Organizer uploads the exported file into the local application.
4. The app parses all submissions into an import preview.
5. Organizer imports every non-duplicate seller draft that has a first name and last name.
6. Organizer handles duplicate rows from the import result when there is exactly one existing seller match, or ignores ambiguous duplicate rows.

There is no public endpoint, webhook, API callback, or internet exposure of the sale tool in this design.

The important constraint is that import creates `deposit_pending` drafts only. Imported sellers and items are not available for checkout until the normal deposit validation gate prints the contract, confirms the signed deposit, moves accepted items to `in_stock`, and locks the seller.

## Provider Direction

The first supported providers should be Google Forms and Framaforms/Yakforms because they can be used without paid third-party dependencies and can export responses to files.

The app should not depend on provider APIs for the first slice. A file import keeps the workflow asynchronous, simple to recover, and aligned with the current event operations model.

Provider observations:

- Google Forms can store responses in a linked spreadsheet and download responses as CSV.
- Framaforms/Yakforms can export results in delimited formats.
- Yakforms documents a long-text component (`Zone de texte`), field groups, grids, conditional fields, and exports. Its public documentation does not describe a dynamic repeatable "add another item" row component.
- CSV can contain multiline fields when the field is quoted. The importer must use a real CSV/TSV parser and must not split the raw file by line or comma manually.

References:

- Google Forms responses: <https://support.google.com/docs/answer/139706>
- Google Forms linked spreadsheets: <https://support.google.com/docs/answer/2917686>
- Yakforms components: <https://docs.yakforms.org/composants/>
- Yakforms exports: <https://docs.yakforms.org/features/exporter-ses-donnees/>
- CSV multiline fields: <https://www.rfc-editor.org/rfc/rfc4180>

## Form Contract

The public form should ask for:

- first name
- last name
- phone, when available
- postal address, when available
- postal code, when available
- city, when available
- email, optional
- notes, optional
- articles, as one multiline text answer

The import minimum is first name and last name. Phone, postal address, postal code, city, email, notes, and valid item lines can be missing at import time. The operator completes required contact fields and accepted items before validating the physical deposit.

The `Articles` answer uses one item per non-empty line. The seller enters the item title first and the whole-euro price last.

Example:

```text
Puzzle bois 5
Lot Playmobil 12
Camion pompier 8
Déguisement reine des neiges 4 EUR
```

Parsing rules:

- one non-empty line is one candidate item
- the price is the final token or final separated segment
- the title is everything before the price
- accepted prices are whole euros, such as `5`, `5 EUR`, `5 €`, `5,00`, or `5.00`
- prices with cents, such as `5,50`, are rejected for that item line
- a title without a price is an invalid item line
- a price without a title is an invalid item line
- extra blank lines are ignored

Invalid item lines do not block seller import. Valid item lines are imported; invalid item lines are ignored and recorded in import notes. If no valid item line exists, the seller draft is still imported with an import note.

## Import Preview And Result

After upload, the app shows a batch import preview. This is not a full correction center. Its purpose is to show what will be imported, what will be skipped, and why.

Each row shows:

- seller name
- phone
- postal code and city
- missing contact fields, if any
- valid item count
- ignored item-line count
- import result
- available actions

The main action is:

- `Importer les nouvelles soumissions`

This imports every row that can safely become a `deposit_pending` seller draft. Contact completeness is not required at import time because the seller deposit validation gate requires complete phone, postal address, postal code, city, and accepted item rows before the seller can move to stock.

## Import Classifications

`À importer`
:: First name and last name are present, no duplicate seller is found, and the row can be imported as a `deposit_pending` draft. Missing phone, address, postal code, city, email, and item data do not block this classification.

`À importer avec note`
:: The seller draft can be imported, but the import will write `seller.import_notes`. Examples include ignored item lines, a submitted city that could not be reconciled, or no valid item line.

`Doublon`
:: The submitted seller matches an existing seller. The row is not imported automatically.

`Erreur vendeur`
:: First name or last name is missing. The row is not imported by the first slice.

`Importé`
:: The row was imported during the current import session.

`Ignoré`
:: The organizer intentionally skipped the row during the current import session.

`Erreur d'import`
:: The row looked importable, but saving failed. Example causes include a sale phase change, a duplicate created by another operator, or an unexpected database constraint failure.

## Bulk Import Behavior

`Importer les nouvelles soumissions` imports:

- `À importer`
- `À importer avec note`

It skips:

- `Doublon`
- `Erreur vendeur`
- `Importé`
- `Ignoré`

After import, the page stays open and highlights imported rows, duplicate rows, and seller errors.

Example summary:

```text
34 dossiers importés.
2 doublons possibles.
3 dossiers sans prénom ou nom.
1 dossier ignoré.
```

## Postal Code And City Handling

The import must not block seller creation on postal-code/city quality. The physical deposit validation is the later gate where the operator completes or fixes contact details with the seller.

Rules:

- If the submitted postal code and city can be reconciled to a canonical city, store the postal code and canonical city.
- If a postal code is present but the city cannot be reconciled, store the postal code, leave `city` blank, and write the submitted city into `seller.import_notes`.
- If the city is present but the postal code is blank, leave `city` blank and write the submitted city into `seller.import_notes`.
- If postal code and city are both blank, leave both blank and do not write a city note.

Example note:

```text
Import formulaire : ville reçue non reconnue pour le code postal 38120 : "ST EGREVE".
```

The first slice does not need inline city correction actions in the import screen.

## Duplicate Handling

Duplicate detection must reuse the application seller duplicate rules:

- when the submitted phone is present, match on normalized first name, normalized last name, and trimmed phone
- when the submitted phone is blank, match on normalized first name and normalized last name only
- email, postal address, postal code, city, and notes are not duplicate identity fields

Duplicate rows are flagged and are not auto-imported. The importer must not create a second seller record when the shared intake service reports a duplicate.

For a duplicate row with exactly one existing seller match, the row shows the submitted seller and the existing seller.

Example:

```text
Soumission :
Sophie Martin - 0612345678

Vendeur existant :
SM042 - Sophie Martin - 0612345678 - 12 articles
```

Actions:

- `Ajouter les articles au vendeur existant`
- `Ignorer cette soumission`

The first slice should not include `Créer un nouveau vendeur malgré le doublon`.

When several duplicate matches are found, the row is treated as an ambiguous edge case. The only available action is:

- `Ignorer cette soumission`

The first slice should not implement a multi-match merge or selection workflow. Multiple matches should be rare because duplicate prevention already blocks normal create/edit paths.

When appending to an existing seller:

- valid item lines are added to that seller as preregistered items, unless an item with the same imported title and same seller price already exists on that seller
- invalid item lines are ignored
- ignored item lines are recorded in `seller.import_notes`
- submitted seller contact fields do not overwrite the existing seller
- the import row becomes `Importé`

Duplicate item detection during append is intentionally narrow. An imported item line is considered already present only when the existing seller already has the same title after normal import trimming and the same seller price in cents. Case, accents, and punctuation are meaningful.

## Bad Item Lines

Bad item lines do not block seller import.

Example row detail:

```text
8 articles prêts, 2 lignes ignorées.
Ligne 4 : prix manquant - Camion rouge
Ligne 7 : prix non accepté - Puzzle 4,50
```

If at least one bad item line exists, the first slice writes durable seller import notes. If no valid item line exists, the seller draft is still imported and the note says that no valid item was imported from the submission.

## Import Notes

The first slice should add one seller-level import notes field, not a full import tracking system.

Suggested field:

```text
seller.import_notes
```

Rules:

- empty or null `import_notes` means no import warning
- non-empty `import_notes` shows `Import à vérifier` on seller detail
- seller edit lets the organizer clear or update the notes
- there is no import status enum
- there is no persistent import batch table

Example notes:

```text
Import formulaire : 2 lignes articles ignorées.
Ligne 4 : prix manquant - Camion rouge
Ligne 7 : prix non accepté - Puzzle 4,50
```

```text
Import formulaire : ville reçue non reconnue pour le code postal 38120 : "ST EGREVE".
```

```text
Import formulaire : article déjà présent ignoré - Puzzle bois - 5 €.
```

## Persistence And Data Flow

The import preview state can be local to the current browser/page for the first implementation. Only imported sellers, imported items, and seller import notes are persisted.

The app does not need:

- import history
- persistent import batches
- stored provider files
- API credentials
- provider webhooks
- a public internet endpoint

Re-importing the same export is acceptable. Duplicate detection catches already-created sellers. The organizer can ignore those duplicate rows or append items when that is intentional.

When a row is imported, writes should reuse existing application services instead of bypassing domain rules:

- seller creation
- duplicate seller detection
- seller code generation
- item reference generation
- item price validation
- sale phase lock checks

Imported sellers start as `deposit_pending` drafts. Imported items start as preregistered items because the seller has not physically delivered the toys yet. Imported sellers default paid seller fee to `EUR 0` until an operator records the paid amount.

## Implementation Slices

### Slice 1: File Import And Local Result

- import CSV/TSV export
- parse provider-neutral seller columns
- parse multiline item field
- classify rows in an import preview
- bulk-import non-duplicate rows with first name and last name
- store incomplete or suspicious seller data as draft fields plus `seller.import_notes`
- show `Import à vérifier` on seller detail when import notes are present, and let organizers clear or edit the notes
- keep submitted postal code while blanking unreconciled submitted city into `seller.import_notes`
- support duplicate append or ignore when exactly one duplicate seller matches
- expose only ignore when several duplicate sellers match
- ignore already-existing item lines by exact title and price when appending duplicate submissions to an existing seller
- ignore bad item lines while importing valid seller data and valid item lines
- keep import results local to the current import session

### Later Optional Slices

- provider-specific column mapping templates
- better CSV/TSV delimiter detection
- import preview export or printout
- stronger duplicate resolution if real operations need it
- optional API integration if file export proves too manual
