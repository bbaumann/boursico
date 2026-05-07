using System.Globalization;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Checkout.Sales;

public sealed class IndexModel(SalesViewService salesViewService) : PageModel
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public IReadOnlyList<SalesListItem> Sales { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Sales = await salesViewService.ListSalesAsync(cancellationToken);
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{priceCents / 100m:0.00} €";
    }

    public static string FormatSoldAt(DateTime soldAt)
    {
        return soldAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", FrenchCulture);
    }
}
