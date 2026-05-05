namespace AssociationSaleTool.Web.Services.Models;

public sealed record CreateSellerWithItemsRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? PostalAddress,
    string? Notes,
    IntakeMode IntakeMode,
    IReadOnlyList<CreateItemRowRequest> Rows,
    int FeePaidCents = 0);
