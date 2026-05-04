using AssociationSaleTool.Web.Services.Models;

namespace AssociationSaleTool.Web.Controllers.Models;

public sealed class CheckoutCartRequest
{
    public List<long> ItemIds { get; set; } = [];

    public string? ReceiptDetails { get; set; }

    public string? PaymentMethod { get; set; } = SalePaymentMethods.Card;

    public string? ChequeNumber { get; set; }

    public string? ChequeBankName { get; set; }
}
