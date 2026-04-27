namespace AssociationSaleTool.Web.Services.Models;

public sealed record UpdateSellerRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Notes);
