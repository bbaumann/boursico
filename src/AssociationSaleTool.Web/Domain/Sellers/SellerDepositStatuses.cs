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

    // A third, display-only state: a pending deposit whose toys are not
    // labelled yet. The stored status keeps two values because every
    // server-side rule only cares whether the deposit is validated.
    public static string ToFrenchLabel(string status, DateTime? depositStartedAt)
    {
        return status == Pending && depositStartedAt is null
            ? "Dépôt non commencé"
            : ToFrenchLabel(status);
    }

    public static string ToCssClass(string status, DateTime? depositStartedAt)
    {
        return status switch
        {
            Validated => "seller-deposit-status-validated",
            Pending when depositStartedAt is null => "seller-deposit-status-not-started",
            _ => "seller-deposit-status-pending"
        };
    }
}
