namespace AssociationSaleTool.Web.Data.Entities;

public sealed class SellerEntity
{
    public long Id { get; set; }

    public string SellerCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Notes { get; set; }

    public List<ItemEntity> Items { get; set; } = [];
}
