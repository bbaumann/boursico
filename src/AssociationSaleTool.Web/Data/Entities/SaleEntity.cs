namespace AssociationSaleTool.Web.Data.Entities;

public sealed class SaleEntity
{
    public long Id { get; set; }

    public DateTime SoldAt { get; set; }

    public int SubtotalSellerPriceCents { get; set; }

    public int CheckoutFeeCents { get; set; }

    public int TotalPaidCents { get; set; }

    public string? BuyerName { get; set; }

    public string? BuyerPostalCode { get; set; }

    public string? BuyerCity { get; set; }

    public string? BuyerAddress { get; set; }

    public string? PaymentMethod { get; set; }

    public string? ChequeNumber { get; set; }

    public string? ChequeBankName { get; set; }

    public List<SaleLineEntity> SaleLines { get; set; } = [];
}
