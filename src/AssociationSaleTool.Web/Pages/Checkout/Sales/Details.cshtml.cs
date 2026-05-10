using System.Globalization;
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Pages.Checkout.Sales;

public sealed class DetailsModel(SalesViewService salesViewService, IOptions<PrintingOptions> printingOptions) : PageModel
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private const string MissingInvoiceAddressMessage = "La facture ne peut pas être imprimée parce que l'adresse n'a pas été saisie au moment de la vente.";

    public SaleDetail Sale { get; private set; } = null!;

    public PrintingOptions Printing => printingOptions.Value;

    public bool ShouldPrintInvoice { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id, bool imprimerFacture = false, CancellationToken cancellationToken = default)
    {
        var sale = await salesViewService.GetSaleAsync(id, cancellationToken);
        if (sale is null)
        {
            return NotFound();
        }

        Sale = sale;
        ShouldPrintInvoice = imprimerFacture && sale.CanPrintInvoice && sale.HasInvoice;
        return Page();
    }

    public async Task<IActionResult> OnPostPrintInvoiceAsync(long id, CancellationToken cancellationToken)
    {
        var result = await salesViewService.EnsureInvoiceAsync(id, cancellationToken);
        if (result == SaleInvoicePrintResult.NotFound)
        {
            return NotFound();
        }

        if (result == SaleInvoicePrintResult.MissingBuyerAddress)
        {
            var sale = await salesViewService.GetSaleAsync(id, cancellationToken);
            if (sale is null)
            {
                return NotFound();
            }

            Sale = sale;
            ModelState.AddModelError("Invoice", MissingInvoiceAddressMessage);
            return Page();
        }

        return Redirect($"/caisse/ventes/{id}?imprimerFacture=true");
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{priceCents / 100m:0.00} €";
    }

    public static string FormatSoldAt(DateTime soldAt)
    {
        return soldAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", FrenchCulture);
    }

    public static string FormatCorrectedAt(DateTime correctedAt)
    {
        return correctedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", FrenchCulture);
    }

    public static string FormatInvoiceIssuedAt(DateTime issuedAt)
    {
        return issuedAt.ToLocalTime().ToString("dd/MM/yyyy", FrenchCulture);
    }
}
