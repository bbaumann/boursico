using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Reconciliation;

public sealed class SheetsModel(ReconciliationService reconciliationService) : PageModel
{
    public IReadOnlyList<SellerSheet> Sheets { get; private set; } = [];

    public string? RequestedSellerCode { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? codeVendeur, CancellationToken cancellationToken)
    {
        if (!ReconciliationAdminSession.IsUnlocked(HttpContext))
        {
            return Redirect("/rapprochement");
        }

        RequestedSellerCode = string.IsNullOrWhiteSpace(codeVendeur)
            ? null
            : codeVendeur.Trim().ToUpperInvariant();

        Sheets = await reconciliationService.ListSellerSheetsAsync(RequestedSellerCode, cancellationToken);

        return Page();
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{priceCents / 100m:0.00} EUR";
    }
}
