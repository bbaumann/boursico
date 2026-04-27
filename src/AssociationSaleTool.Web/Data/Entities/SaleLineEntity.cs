namespace AssociationSaleTool.Web.Data.Entities;

public sealed class SaleLineEntity
{
    public long Id { get; set; }

    public long SaleId { get; set; }

    public long ItemId { get; set; }

    public int SellerPriceAtSaleCents { get; set; }

    public SaleEntity Sale { get; set; } = null!;

    public ItemEntity Item { get; set; } = null!;
}
