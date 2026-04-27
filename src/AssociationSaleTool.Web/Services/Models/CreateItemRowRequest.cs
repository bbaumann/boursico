namespace AssociationSaleTool.Web.Services.Models;

public sealed record CreateItemRowRequest(
    string Title,
    int SellerPriceCents);
