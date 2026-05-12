namespace AssociationSaleTool.Web.Services.Models;

public sealed record UpdateSellerRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? PostalAddress,
    string? Notes,
    int FeePaidCents = 0,
    string? PostalCode = null,
    string? City = null,
    string? ImportNotes = null,
    string? IdentityDocumentType = null,
    string? IdentityDocumentNumber = null,
    DateOnly? IdentityDocumentIssuedOn = null,
    string? IdentityDocumentIssuer = null);
