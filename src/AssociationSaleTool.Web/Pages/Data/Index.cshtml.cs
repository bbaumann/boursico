using System.Globalization;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Data;

public sealed class IndexModel(DataListService dataListService) : PageModel
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private const string ArticlesTab = "articles";
    private const string SellersTab = "vendeurs";
    private const string SalesTab = "ventes";
    private const string ChequesTab = "cheques";

    public string ActiveTab { get; private set; } = ArticlesTab;

    public IReadOnlyList<DataListItemRow> Items { get; private set; } = [];

    public IReadOnlyList<DataListSellerRow> Sellers { get; private set; } = [];

    public IReadOnlyList<DataListSaleRow> Sales { get; private set; } = [];

    public IReadOnlyList<DataListChequeRow> Cheques { get; private set; } = [];

    public async Task OnGetAsync(string? onglet, CancellationToken cancellationToken)
    {
        ActiveTab = NormalizeTab(onglet);

        switch (ActiveTab)
        {
            case ArticlesTab:
                Items = await dataListService.ListItemsAsync(cancellationToken);
                break;
            case SellersTab:
                Sellers = await dataListService.ListSellersAsync(cancellationToken);
                break;
            case SalesTab:
                Sales = await dataListService.ListSalesAsync(cancellationToken);
                break;
            case ChequesTab:
                Cheques = await dataListService.ListChequesAsync(cancellationToken);
                break;
        }
    }

    public bool IsActiveTab(string tab)
    {
        return string.Equals(ActiveTab, tab, StringComparison.Ordinal);
    }

    public string TabClass(string tab)
    {
        return IsActiveTab(tab)
            ? "button-link button-link-solid"
            : "button-link";
    }

    public string? TabAriaCurrent(string tab)
    {
        return IsActiveTab(tab) ? "page" : null;
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{priceCents / 100m:0.00} €";
    }

    public static string FormatSellerFeeProgress(int paidFeeCents, int expectedFeeCents)
    {
        return $"{paidFeeCents / 100}/{expectedFeeCents / 100}\u20ac";
    }

    public static string FormatSoldAt(DateTime soldAt)
    {
        return soldAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", FrenchCulture);
    }

    private static string NormalizeTab(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            SellersTab => SellersTab,
            SalesTab => SalesTab,
            ChequesTab => ChequesTab,
            _ => ArticlesTab
        };
    }
}
