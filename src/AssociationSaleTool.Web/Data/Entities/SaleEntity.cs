namespace AssociationSaleTool.Web.Data.Entities;

public sealed class SaleEntity
{
    public long Id { get; set; }

    public DateTime SoldAt { get; set; }

    public int TotalAmountCents { get; set; }

    public string? ReceiptDetails { get; set; }

    public List<SaleLineEntity> SaleLines { get; set; } = [];
}
