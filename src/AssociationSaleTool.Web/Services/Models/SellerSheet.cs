namespace AssociationSaleTool.Web.Services.Models;

public sealed record SellerSheet(
    long SellerId,
    string SellerCode,
    string SellerName,
    string? Email,
    string Phone,
    string PostalAddress,
    string PostalCode,
    string City,
    IReadOnlyList<SellerSheetItem> PayableItems,
    IReadOnlyList<SellerSheetItem> ReturnedItems,
    int ItemSubtotalCents,
    int ExpectedFeeCents,
    int PaidFeeCents,
    int RemainingFeeCents,
    int TotalPayableCents);
