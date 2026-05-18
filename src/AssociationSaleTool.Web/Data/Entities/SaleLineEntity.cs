namespace AssociationSaleTool.Web.Data.Entities;

public sealed class SaleLineEntity
{
    public long Id { get; set; }

    public long SaleId { get; set; }

    public long ItemId { get; set; }

    public int SellerPriceAtSaleCents { get; set; }

    public DateTime? CorrectedAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public int? RefundItemPriceCents { get; set; }

    public int? RefundCheckoutFeeCents { get; set; }

    public int? RefundTotalCents { get; set; }

    public string? RefundReason { get; set; }

    public string? RefundCheckoutCode { get; set; }

    public SaleEntity Sale { get; set; } = null!;

    public ItemEntity Item { get; set; } = null!;
}
