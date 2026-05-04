namespace AssociationSaleTool.Web.Controllers.Models;

public sealed class CheckoutCartRequest
{
    public List<long> ItemIds { get; set; } = [];

    public string? BuyerName { get; set; }

    public string? BuyerPostalCode { get; set; }

    public string? BuyerCity { get; set; }

    public string? ReceiptDetails { get; set; }
}
