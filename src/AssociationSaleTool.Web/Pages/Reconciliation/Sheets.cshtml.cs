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

    public long? RequestedSellerId { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(long? vendeur, CancellationToken cancellationToken)
    {
        if (!ReconciliationAdminSession.IsUnlocked(HttpContext))
        {
            return Redirect("/rapprochement");
        }

        if (await salePhaseService.GetCurrentPhaseAsync(cancellationToken) != SalePhase.PostSale)
        {
            return Redirect("/rapprochement");
        }

        RequestedSellerId = vendeur;

        try
        {
            Sheets = await reconciliationService.ListSellerSheetsAsync(RequestedSellerId, cancellationToken);
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
}
