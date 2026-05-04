namespace AssociationSaleTool.Web.Services.Models;

public sealed record SellerDuplicateMatch(
    long SellerId,
    string SellerCode,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    int ItemCount,
    SellerDuplicateMatchReason Reason);
