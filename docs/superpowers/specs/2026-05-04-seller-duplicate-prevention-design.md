# Seller Duplicate Prevention Design

## Goal

Prevent operators from accidentally creating or editing a seller into a duplicate identity.

This current V1 slice keeps seller management focused on duplicate prevention. It does not broaden the seller workflow into cleanup tooling, seller fee management, merge/delete/archive operations, or live search UX.

## Scope

This slice includes:

- blocking duplicate checks when creating a seller
- blocking duplicate checks when editing seller identity fields
- a reusable seller duplicate-check service
- structured duplicate results that page models can render in French
- an integration contract for Excel seller/item prefill
- tests for normalization, matching, service behavior, and page behavior

This slice does not include:

- seller merge
- seller deletion or archive
- admin override to create anyway
- live duplicate warnings while typing
- cleanup of duplicate sellers already present in the database
- seller fee management
- Excel parsing or Excel upload implementation
- schema changes for normalized duplicate keys

## Existing Context

The application already has a French seller workflow:

- `/vendeurs` lists sellers
- `/vendeurs/creer` creates a seller
- `/vendeurs/{id}` shows seller details and item intake
- `/vendeurs/{id}/modifier` edits seller information

Seller and item master data are editable only while the sale phase allows master-data edits. This feature keeps that phase rule intact and adds duplicate prevention inside the same create/edit save paths.

The existing Excel seller/item prefill design is documentation only. This duplicate-prevention slice should not implement Excel, but it should define the behavior that Excel will later reuse.

## Matching Rules

Duplicate matching uses exact normalized identity data. It is intentionally not fuzzy matching.

### Name Normalization

For first name and last name:

- trim leading and trailing spaces
- collapse repeated internal spaces to one space
- compare case-insensitively
- compare accent-insensitively
- keep punctuation meaningful
- keep hyphens meaningful
- keep apostrophes meaningful
- keep word spacing meaningful

Examples:

- `Émilie` and `Emilie` match
- `Jean  Luc` and `jean luc` match
- `Jean-Luc`, `Jean Luc`, and `Jeanluc` do not all collapse into one key

### Phone Normalization

For phone numbers:

- trim leading and trailing spaces only
- do not remove spaces
- do not remove punctuation
- do not remove dots, slashes, hyphens, or parentheses
- do not convert between `+33` and local `0` French formats

### Duplicate Criteria

When the submitted phone is present:

- normalized first name must match
- normalized last name must match
- trimmed phone must match

When the submitted phone is blank:

- normalized first name and normalized last name are enough to block
- existing sellers with the same normalized name match even if they have a phone number
- the UI message must state that the block is based on name only because no phone was entered

E-mail is not part of duplicate matching. It is display context only.

Edit checks must exclude the seller currently being edited.

If several sellers match, the service returns all matches ordered by seller code.

## Operator UX

Duplicate detection runs only when the operator submits a form. There is no live warning while typing.

### Create Seller

On `/vendeurs/creer`, if duplicate sellers are found:

- stay on the create page
- do not create a seller
- preserve the submitted form fields
- show a blocking French validation area
- show all matching sellers

Each matching seller entry should show:

- seller code
- full name
- phone when available
- e-mail when available
- item count
- a clear link or button to open the existing seller

There is no `create anyway` action.

### Edit Seller

On `/vendeurs/{id}/modifier`, if the edited identity would duplicate another seller:

- stay on the edit page
- do not save the edits
- preserve the submitted form fields
- show the same kind of blocking French match list

The currently edited seller is not considered a duplicate of itself.

### Name-Only Blocks

When the submitted phone is blank and matching is name-only, the message should explicitly tell the operator:

- the matching seller was found from the name
- no phone was entered for the submitted seller
- the operator must verify manually against the existing seller or enter a distinguishing phone value

## Future Excel Contract

When Excel seller/item prefill is implemented later, it should use the same duplicate-check service.

Expected future behavior:

- Excel parsing remains a prefill step and does not persist seller or item data
- if parsed seller data matches an existing seller, redirect to `/vendeurs/{id}`
- carry parsed item rows as pending rows to the existing seller detail page
- show a French alert explaining that the seller already exists and the Excel rows were added to the item form for review
- the operator still reviews and saves the item rows manually

This spec records the contract only. It does not implement Excel parsing, upload, pending-row session handoff, or combined seller/item save.

## Service Design

Add a small seller duplicate-check service under the application service layer.

Suggested shape:

```csharp
public sealed record SellerDuplicateCheckRequest(
    string FirstName,
    string LastName,
    string? Phone,
    long? ExcludedSellerId = null);

public sealed record SellerDuplicateMatch(
    long SellerId,
    string SellerCode,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    int ItemCount,
    SellerDuplicateMatchReason Reason);

public enum SellerDuplicateMatchReason
{
    NameAndPhone,
    NameOnlySubmittedPhoneMissing
}
```

The service should:

1. load sellers with item counts
2. normalize submitted values
3. normalize existing seller values in application code
4. exclude `ExcludedSellerId` when present
5. return matching sellers ordered by seller code

The database schema stays unchanged. Normalized columns and indexes are not needed for this small event database.

## Enforcement

Duplicate prevention should be authoritative server-side behavior, not just a page-level warning.

Recommended enforcement:

- `IntakeService.CreateSellerAsync` checks duplicates before creating
- `IntakeService.UpdateSellerAsync` checks duplicates before saving edits
- service result types expose duplicate matches so Razor Pages can render operator-friendly details

The final implementation may introduce result types such as:

- `CreateSellerResult`
- `UpdateSellerResult` with duplicate matches

The important contract is that future callers cannot bypass duplicate prevention by calling `IntakeService` directly.

## Error Handling

Duplicate detection should not throw for normal duplicate outcomes. It should return structured results that the page can display.

The page messages must be French and blocking.

Suggested message for name-and-phone matches:

`Un vendeur existe déjà avec les mêmes nom, prénom et téléphone. Ouvrez le vendeur existant avant de continuer.`

Suggested message for name-only matches:

`Un vendeur existe déjà avec les mêmes nom et prénom. Aucun téléphone n'a été saisi, vérifiez manuellement le vendeur existant ou ajoutez un téléphone distinct.`

If multiple matches exist, the page should show a list under the message.

## Testing Strategy

Service/unit tests should cover:

- accent-insensitive name matching
- case-insensitive name matching
- repeated-space normalization
- punctuation and hyphen preservation
- phone trimming only
- phone-present duplicate requires matching trimmed phone
- phone-present different phone does not match
- phone-blank submission blocks same-name sellers
- phone-blank submission matches same-name sellers that already have a phone
- e-mail does not affect matching
- edit excludes the current seller id
- multiple matches are returned in stable order with item counts

Application/page tests should cover:

- create blocks duplicate seller and preserves submitted fields
- create renders all duplicate matches with seller code, name, phone/e-mail, item count, and link
- create uses the name-only warning when submitted phone is blank
- edit blocks changing a seller into another seller's identity
- edit allows saving when only the current seller matches
- non-duplicate create/edit still succeeds
- French validation messages are present

## Documentation Updates For Implementation

When implemented, update:

- `docs/vnext-backlog.md` to mark duplicate seller prevention as implemented
- `docs/features.md` if duplicate prevention becomes part of the supported event-operations workflow
- `docs/domain-model-and-references.md` only if the seller identity semantics need to be documented there
- `docs/decisions.md` if the implementation introduces a notable architectural decision beyond this spec
