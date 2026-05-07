namespace AssociationSaleTool.Web.Configuration;

public sealed class PrintingOptions
{
    public const string SectionName = "Printing";

    public string InvoicePeriod { get; init; } = "2611";

    public string InvoiceCode { get; init; } = "AFBJ";

    public string SellerName { get; init; } = "Association Familiale";

    public string SellerAddress { get; init; } = "33 Av. de l'Europe, 38120 Saint-Égrève";

    public string SellerSiret { get; init; } = "306 041 997 00058";

    public string SellerLegalStatus { get; init; } = "Association loi 1901";

    public string VatExemptionText { get; init; } = "TVA non applicable en vertu de l'article 293 B du CGI.";

    public string LogoPath { get; init; } = "/images/logo-afse.jpg";

    public string InvoiceNumberPrefix => $"{InvoicePeriod.Trim()}{InvoiceCode.Trim().ToUpperInvariant()}";
}
