namespace AssociationSaleTool.Web.Data.Entities;

public sealed class CashRegisterEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int InitialCashCents { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public List<SaleEntity> Sales { get; set; } = [];

    public List<SaleLineEntity> RefundSaleLines { get; set; } = [];
}
