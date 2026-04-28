using System.Text.Json;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Checkout;

public sealed class IndexModel(CheckoutService checkoutService) : PageModel
{
    public string CatalogJson { get; private set; } = "[]";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var catalog = await checkoutService.ListCatalogItemsAsync(cancellationToken);
        CatalogJson = JsonSerializer.Serialize(catalog, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
