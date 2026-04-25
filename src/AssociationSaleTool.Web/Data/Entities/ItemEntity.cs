namespace AssociationSaleTool.Web.Data.Entities;

public sealed class ItemEntity
{
    public long Id { get; set; }

    public long SellerId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public int ItemNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public int SellerPriceCents { get; set; }

    public int SalePriceCents { get; set; }

    public string Status { get; set; } = string.Empty;

    public SellerEntity Seller { get; set; } = null!;

    public SaleLineEntity? SaleLine { get; set; }
}
