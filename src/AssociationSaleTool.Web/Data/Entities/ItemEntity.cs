namespace AssociationSaleTool.Web.Data.Entities;

public sealed class ItemEntity
{
    public long Id { get; set; }

    public long SellerId { get; set; }

    /// <summary>
    /// Null until the seller's deposit is started at a desk: the start gives
    /// the existing rows a consecutive run from the desk's letters, and a row
    /// added after the start gets its reference as it is saved. An item
    /// without a reference is necessarily still "registered" under a
    /// "deposit_pending" seller, so it can never reach checkout.
    /// </summary>
    public string? Reference { get; set; }

    public string Title { get; set; } = string.Empty;

    public int? SellerPriceCents { get; set; }

    public string Status { get; set; } = string.Empty;

    public SellerEntity Seller { get; set; } = null!;

    public List<SaleLineEntity> SaleLines { get; set; } = [];
}
