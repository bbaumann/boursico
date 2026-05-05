namespace AssociationSaleTool.Web.Services.Models;

public sealed record CreateSellerRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? PostalAddress,
    string? Notes,
    int FeePaidCents = 0,
    string? PostalCode = null,
    string? City = null);
