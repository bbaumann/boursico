using System.Text.Json;
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Pages.Checkout;

public sealed class IndexModel(
    CheckoutService checkoutService,
    SalePhaseService salePhaseService,
    IOptions<PrintingOptions> printingOptions) : PageModel
{
    public string CatalogJson { get; private set; } = "[]";

    public string ReceiptHeaderText { get; } = printingOptions.Value.ReceiptHeaderText;

    public bool SaleAllowed { get; private set; }

    public string SalePhaseLabel { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var phase = await salePhaseService.GetCurrentPhaseAsync(cancellationToken);
        SaleAllowed = phase != SalePhase.PostSale;
        SalePhaseLabel = SalePhaseDisplay.ToFrenchLabel(phase);

        if (!SaleAllowed)
        {
            return;
        }

        var catalog = await checkoutService.ListCatalogItemsAsync(cancellationToken);
        CatalogJson = JsonSerializer.Serialize(catalog, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
