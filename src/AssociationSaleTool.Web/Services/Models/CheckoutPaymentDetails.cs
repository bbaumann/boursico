namespace AssociationSaleTool.Web.Services.Models;

public sealed record CheckoutPaymentDetails(
    string? PaymentMethod,
    string? ChequeNumber = null,
    string? ChequeBankName = null);

public static class SalePaymentMethods
{
    public const string Card = "card";
    public const string Cash = "cash";
    public const string Cheque = "cheque";

    public static string? Normalize(string? paymentMethod)
    {
        var normalized = paymentMethod?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "cb" or Card => Card,
            "especes" or "espèces" or Cash => Cash,
            "cheque" or "chèque" or Cheque => Cheque,
            _ => null
        };
    }

    public static string GetDisplayLabel(string? paymentMethod)
    {
        return paymentMethod switch
        {
            Card => "CB",
            Cash => "Espèces",
            Cheque => "Chèque",
            _ => "Non renseigné"
        };
    }
}
