namespace AssociationSaleTool.Web.Services.Models;

public sealed record SellerDuplicateCheckRequest(
    string FirstName,
    string LastName,
    string? Phone,
    long? ExcludedSellerId = null);
