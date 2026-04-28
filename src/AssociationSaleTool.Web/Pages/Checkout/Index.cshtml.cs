using System.Text.Json;
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Pages.Checkout;

public sealed class IndexModel(
    CheckoutService checkoutService,
    IOptions<PrintingOptions> printingOptions) : PageModel
{
    public string CatalogJson { get; private set; } = "[]";

    public string ReceiptHeaderText { get; } = printingOptions.Value.ReceiptHeaderText;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var catalog = await checkoutService.ListCatalogItemsAsync(cancellationToken);
        CatalogJson = JsonSerializer.Serialize(catalog, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
