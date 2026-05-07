using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Reconciliation;

public sealed class SheetsModel(
    ReconciliationService reconciliationService,
    SalePhaseService salePhaseService) : PageModel
{
    public IReadOnlyList<SellerSheet> Sheets { get; private set; } = [];

    public string? RequestedSellerCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? codeVendeur, CancellationToken cancellationToken)
    {
        if (!ReconciliationAdminSession.IsUnlocked(HttpContext))
        {
            return Redirect("/rapprochement");
        }

        if (await salePhaseService.GetCurrentPhaseAsync(cancellationToken) != SalePhase.PostSale)
        {
            return Redirect("/rapprochement");
        }

        RequestedSellerCode = NormalizeRequestedSellerCode(codeVendeur);

        try
        {
            Sheets = await reconciliationService.ListSellerSheetsAsync(RequestedSellerCode, cancellationToken);
        }
        catch (ReconciliationSellerSheetDataException exception)
        {
            ErrorMessage =
                $"Données de vente incohérentes pour l'article {exception.ItemReference}. La feuille vendeur ne peut pas être imprimée.";
            Sheets = [];
        }

        return Page();
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{priceCents / 100m:0.00} €";
    }

    private static string? NormalizeRequestedSellerCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmedValue = value.Trim();
        var codeSeparatorIndex = trimmedValue.IndexOf(" - ", StringComparison.Ordinal);
        var sellerCode = codeSeparatorIndex < 0
            ? trimmedValue
            : trimmedValue[..codeSeparatorIndex];

        return sellerCode.ToUpperInvariant();
    }
}
