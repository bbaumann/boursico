# Seller Self-Service Form Import

## Purpose

This document captures the VNext feature request for seller self-service intake through an external online form provider.

The application must remain private on the local network. The online form provider is only a seller-facing collection surface. Data enters the local application through a file export/import workflow controlled by organizers.

## Recommended Shape

Use a provider-neutral file import:

1. Sellers submit an online form.
2. Organizer exports responses from the provider as CSV, TSV, or a spreadsheet format.
3. Organizer uploads the exported file into the local application.
4. The app parses all submissions into a review table.
5. Organizer bulk-imports ready submissions.
6. Organizer fixes remaining submissions row by row.

There is no public endpoint, webhook, API callback, or internet exposure of the sale tool in this design.

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
- phone
- postal address
- postal code
- city
- email, optional
- notes, optional
- articles, as one multiline text answer

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

Invalid item lines do not block seller import. Valid item lines are imported; invalid item lines are ignored and reported.

## Review Screen

After upload, the app shows a batch review table.

Each row shows:

- seller name
- phone
- postal code and city
- valid item count
- ignored item-line count
- status
- available actions

The main action is:

- `Importer tous les dossiers prêts`

This imports all rows that can be imported without organizer judgment. Rows that need action remain visible.

## Review Statuses

`Prêt`
: Seller data is valid, postal code/city is accepted or safely normalized, no duplicate action is required, and the row can be bulk imported.

`Prêt avec avertissements`
: Seller data can be imported, but some item lines will be ignored. Bulk import is allowed. The ignored lines are shown in the row detail.

`Ville à corriger`
: The postal code/city pair is invalid, ambiguous, or unsupported. The organizer must fix it before import.

`Doublon possible`
: The submitted seller matches an existing seller. The organizer must choose whether to append valid items to the existing seller or ignore the submission.

`Erreur vendeur`
: Required seller data is missing or invalid. The organizer must fix the row before import.

`Importé`
: The row was imported during the current review session.

`Ignoré`
: The organizer intentionally skipped the row during the current review session.

`Erreur d'import`
: The row looked ready, but saving failed. Example causes include a sale phase change or an unexpected database constraint failure.

## Bulk Import Behavior

`Importer tous les dossiers prêts` imports:

- `Prêt`
- `Prêt avec avertissements`

It skips:

- `Ville à corriger`
- `Doublon possible`
- `Erreur vendeur`
- `Déjà importé` if that transient classification is shown later
- `Ignoré`

After bulk import, the page stays open and filters or highlights unresolved rows.

Example summary:

```text
34 dossiers importés.
3 dossiers à corriger.
2 doublons possibles.
1 dossier ignoré.
```

## City Correction UX

For `Ville à corriger`, the row shows the submitted value and the possible canonical values for that postal code.

Example:

```text
Valeur reçue : 38120 ST EGREVE
Ville reconnue possible : Saint-Égrève
```

Actions:

- `Utiliser Saint-Égrève`
- `Modifier`

`Modifier` opens inline fields for postal code and city. After `Valider la correction`, the row is revalidated. If it is otherwise safe, it becomes `Prêt` or `Prêt avec avertissements`.

Postal-code rules:

- for Isère postal codes, the app validates against `wwwroot/data/isere-postal-cities.json`
- exact canonical matches are accepted
- unambiguous normalized matches may be accepted and stored as the canonical city
- invalid or ambiguous matches block import until corrected
- non-Isère postal code/city values may remain free text unless a later decision narrows the seller geography

## Duplicate UX

For `Doublon possible`, the row shows the submitted seller and matching existing seller records.

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

When appending to an existing seller:

- valid item lines are added to that seller as preregistered items
- invalid item lines are ignored
- if import notes exist in the later slice, ignored lines are recorded on the seller
- the review row becomes `Importé`

## Bad Item Lines

Bad item lines do not block seller import.

Example row detail:

```text
8 articles prêts, 2 lignes ignorées.
Ligne 4 : prix manquant - Camion rouge
Ligne 7 : prix non accepté - Puzzle 4,50
```

The first slice can keep this only in the review result. A later slice should store durable seller import notes.

## Import Notes Slice

A later slice should add one seller-level import notes field, not a full import tracking system.

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

## Persistence And Data Flow

The import review state can be local to the current browser/page for the first implementation.

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

Imported items start as preregistered items because the seller has not physically delivered the toys yet.

## Implementation Slices

### Slice 1: File Import And Local Review

- import CSV/TSV export
- parse provider-neutral seller columns
- parse multiline item field
- classify rows in a review table
- bulk-import ready rows
- support inline city correction
- support duplicate append or ignore
- ignore bad item lines while importing valid seller data and valid item lines
- keep import results local to the current review session

### Slice 2: Seller Import Notes

- add `seller.import_notes`
- write ignored item-line details into import notes when relevant
- show `Import à vérifier` on seller detail when notes are present
- let organizers clear or edit import notes

### Later Optional Slices

- provider-specific column mapping templates
- better CSV/TSV delimiter detection
- import preview export or printout
- stronger duplicate resolution if real operations need it
- optional API integration if file export proves too manual
