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
    private const string MissingInvoiceAddressMessage = "Ajoutez une adresse de facturation avant d'imprimer la facture.";
    private const string MissingBuyerAddressMessage = "L'adresse de facturation est obligatoire pour imprimer une facture.";
    private const string SavedBuyerAddressMessage = "Adresse de facturation enregistrée.";

    public SaleDetail Sale { get; private set; } = null!;

    [BindProperty]
    public BuyerAddressFormInput AddressInput { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public PrintingOptions Printing => printingOptions.Value;

    public bool ShouldPrintInvoice { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id, bool imprimerFacture = false, CancellationToken cancellationToken = default)
    {
        var sale = await salesViewService.GetSaleAsync(id, cancellationToken);
        if (sale is null)
        {
            return NotFound();
        }

        SetSaleForPage(sale);
        ShouldPrintInvoice = imprimerFacture && sale.CanPrintInvoice && sale.HasInvoice;
        return Page();
    }

    public async Task<IActionResult> OnPostEnregistrerAdresseAsync(long id, CancellationToken cancellationToken)
    {
        var submittedBuyerAddress = AddressInput.BuyerAddress;
        var result = await salesViewService.UpdateBuyerAddressAsync(id, submittedBuyerAddress, cancellationToken);
        if (result == SaleBuyerAddressUpdateResult.NotFound)
        {
            return NotFound();
        }

        if (result == SaleBuyerAddressUpdateResult.MissingBuyerAddress)
        {
            var sale = await salesViewService.GetSaleAsync(id, cancellationToken);
            if (sale is null)
            {
                return NotFound();
            }

            SetSaleForPage(sale);
            AddressInput.BuyerAddress = submittedBuyerAddress;
            ModelState.AddModelError("AddressInput.BuyerAddress", MissingBuyerAddressMessage);
            return Page();
        }

        StatusMessage = SavedBuyerAddressMessage;
        return Redirect($"/caisse/ventes/{id}");
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

            SetSaleForPage(sale);
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

    private void SetSaleForPage(SaleDetail sale)
    {
        Sale = sale;
        AddressInput = new BuyerAddressFormInput
        {
            BuyerAddress = sale.BuyerAddress
        };
    }

    public sealed class BuyerAddressFormInput
    {
        public string? BuyerAddress { get; set; }
    }
}
