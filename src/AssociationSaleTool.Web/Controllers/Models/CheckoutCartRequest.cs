namespace AssociationSaleTool.Web.Controllers.Models;

public sealed class CheckoutCartRequest
{
    public List<long> ItemIds { get; set; } = [];

    public string? ReceiptDetails { get; set; }
}
