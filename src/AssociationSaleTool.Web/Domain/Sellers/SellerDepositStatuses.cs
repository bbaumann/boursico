namespace AssociationSaleTool.Web.Domain.Sellers;

public static class SellerDepositStatuses
{
    public const string Pending = "deposit_pending";
    public const string Validated = "deposit_validated";

    public static string ToFrenchLabel(string status)
    {
        return status switch
        {
            Pending => "Dépôt à valider",
            Validated => "Dépôt validé",
            _ => status
        };
    }
}
