namespace AssociationSaleTool.Web.Data.Entities;

public sealed class SellerEntity
{
    public long Id { get; set; }

    public string SellerCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string PostalAddress { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? IdentityDocumentType { get; set; }

    public string? IdentityDocumentNumber { get; set; }

    public DateOnly? IdentityDocumentIssuedOn { get; set; }

    public int FeePaidCents { get; set; }

    public string DepositStatus { get; set; } = "deposit_pending";

    public string? Notes { get; set; }

    public string? ImportNotes { get; set; }

    public List<ItemEntity> Items { get; set; } = [];
}
