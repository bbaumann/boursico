# French UI Requirement Design

## Requirement

All users of the application are French-speaking. The user-facing web application must therefore use French for both visible UI text and user-facing URLs.

## Decision

The application will adopt:

- French labels, page titles, buttons, helper text, and validation messages
- French route templates and navigable URLs

The application will not adopt a full multilingual localization framework in V1.

## Boundary

This requirement applies to:

- Razor page titles and visible content
- form labels
- buttons and links
- validation and error text shown to operators
- user-facing route templates

This requirement does not require renaming:

- C# types
- internal service names
- page file names
- canonical technical documentation language

## Implementation Direction

Use explicit French `@page` route templates for Razor Pages while keeping internal file paths in English. This gives French URLs without creating unnecessary code churn.
