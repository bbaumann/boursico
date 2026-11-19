namespace AssociationSaleTool.Web.Services.Models;

public sealed record UnfinishedDeposit(
    long SellerId,
    string SellerCode,
    string SellerName);
