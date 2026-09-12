using System.Globalization;
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Sellers;
using AssociationSaleTool.Web.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class DepositContractModel(IntakeService intakeService, IOptions<PrintingOptions> printingOptions) : PageModel
{
    public PrintingOptions Printing => printingOptions.Value;

    public SellerEntity? Seller { get; private set; }

    public IReadOnlyList<ItemEntity> Items { get; private set; } = [];

    public int FeePerBlockCents => SellerFeeCalculator.FeePerBlockCents;

    public int ItemsPerFeeBlock => SellerFeeCalculator.ItemsPerFeeBlock;

    public int ExpectedFeeCents { get; private set; }

    public int PaidFeeCents { get; private set; }

    public int RemainingFeeCents { get; private set; }

    public string FeePaymentLabel => RemainingFeeCents == 0 ? "réglé" : "à régler";

    public bool AutoPrint { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id, string? impression, CancellationToken cancellationToken)
    {
        AutoPrint = string.Equals(impression, "auto", StringComparison.OrdinalIgnoreCase);

        var seller = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (seller is null)
        {
            return NotFound();
        }

        Seller = seller;
        Items = seller.Items.OrderBy(x => x.Id).ToList();
        ExpectedFeeCents = SellerFeeCalculator.CalculateExpectedFeeCents(Items.Count);
        PaidFeeCents = seller.FeePaidCents;
        RemainingFeeCents = SellerFeeCalculator.CalculateRemainingFeeCents(ExpectedFeeCents, PaidFeeCents);
        return Page();
    }

    public static string FormatPrice(int? priceCents)
    {
        return priceCents is null
            ? "À compléter"
            : $"{(priceCents.Value / 100m).ToString("0.00", CultureInfo.InvariantCulture)} €";
    }

    public static string FormatDate(DateOnly? date)
    {
        return date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "À compléter";
    }
}
