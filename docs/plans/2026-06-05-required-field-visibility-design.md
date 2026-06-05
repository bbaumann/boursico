# Required Field Visibility Design

## Goal

Make blocking fields easier to identify on the seller create/edit forms and the item edit form without changing existing submit-button behavior.

## Scope

The change applies to:

- `/vendeurs/creer`
- `/vendeurs/{id}/modifier`
- `/articles/{id}/modifier`

The following behaviors stay unchanged:

- `Valider le dépôt` keeps the current deposit-validation flow and blocking message list.
- `Finaliser` on checkout keeps the current cart-driven disabled state and click-time validation.
- Admin phase buttons keep their current disabled state for the active phase.
- Seller and item save buttons remain clickable when the form is editable, so standard validation messages can explain what is missing after submit.

## Design

Required field labels get a small French `Obligatoire` badge. The badge is text, not only an asterisk, so it remains clear for users who do not infer the meaning of `*`.

Fields that are invalid after validation get stronger visual treatment through existing validation semantics: a visible border/accent, a light error background, and stronger validation-message spacing. This keeps the page calm before the user acts, but makes the blocking fields obvious after a failed save.

A short form note explains the convention in French near the form content:

`Les champs marqués Obligatoire doivent être complétés.`

## Accessibility

The visual marker uses readable text instead of color alone. Required inputs should expose `required`/`aria-required` where those fields are truly required for that form submission.

## Testing

Add focused smoke tests that render the seller create page and item edit page and verify required labels expose `Obligatoire`. Existing model validation tests continue to cover server-side rejection of missing values.
