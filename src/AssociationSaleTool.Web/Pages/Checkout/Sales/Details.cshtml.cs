using System.Globalization;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Checkout.Sales;

public sealed class DetailsModel(SalesViewService salesViewService) : PageModel
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public SaleDetail Sale { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var sale = await salesViewService.GetSaleAsync(id, cancellationToken);
        if (sale is null)
        {
            return NotFound();
        }

        Sale = sale;
        return Page();
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{priceCents / 100m:0.00} EUR";
    }

    public static string FormatSoldAt(DateTime soldAt)
    {
        return soldAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", FrenchCulture);
    }

    public static string FormatCorrectedAt(DateTime correctedAt)
    {
        return correctedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", FrenchCulture);
    }
}
