namespace AssociationSaleTool.Web.Data.Entities;

public sealed class ItemEntity
{
    public long Id { get; set; }

    public long SellerId { get; set; }

    /// <summary>
    /// Null until the seller's deposit reaches the contract-print step, when a
    /// contiguous run is allocated. An item without a reference is necessarily
    /// still "registered" under a "deposit_pending" seller, so it can never
    /// reach checkout.
    /// </summary>
    public string? Reference { get; set; }

    public int ItemNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public int? SellerPriceCents { get; set; }

    public string Status { get; set; } = string.Empty;

    public SellerEntity Seller { get; set; } = null!;

    public List<SaleLineEntity> SaleLines { get; set; } = [];
}
