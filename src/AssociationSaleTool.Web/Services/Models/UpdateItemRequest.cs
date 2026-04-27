namespace AssociationSaleTool.Web.Services.Models;

public sealed record UpdateItemRequest(
    string Title,
    int SellerPriceCents);
