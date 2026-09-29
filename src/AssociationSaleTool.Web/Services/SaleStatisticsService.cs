using System.Globalization;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class SaleStatisticsService(AppDbContext dbContext, ReconciliationService reconciliationService)
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private const string InvalidPriceLabel = "Sans prix valide";
    private const string MissingCityLabel = "Non renseignée";

    private static readonly (string Label, int MaxCents)[] PriceBrackets =
    [
        ("1 à 2 €", 200),
        ("3 à 5 €", 500),
        ("6 à 10 €", 1000),
        ("11 à 20 €", 2000),
        ("Plus de 20 €", int.MaxValue)
    ];

    // Seller-side figures (margin, flat fee deductions, amounts owed, sold prices) come from
    // the seller sheets, so the statistics always agree with what sellers are paid.
    public async Task<SaleStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var sheets = await reconciliationService.ListSellerSheetsAsync(cancellationToken: cancellationToken);
        var sales = await dbContext.Sales
            .AsNoTracking()
            .Include(x => x.SaleLines)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var items = await dbContext.Items
            .AsNoTracking()
            .Select(x => new { x.Status, x.SellerPriceCents })
            .ToListAsync(cancellationToken);

        // A payout never goes below zero, so only part of an unpaid fee may be deducted.
        var flatFeeDeductedCents = sheets.Sum(sheet =>
            sheet.ItemSubtotalCents - sheet.AssociationMarginCents - sheet.TotalPayableCents);
        var refundedLines = sales
            .SelectMany(sale => sale.SaleLines)
            .Where(line => line.RefundedAt is not null)
            .ToList();

        var fees = new SaleStatisticsFees(
            sheets.Sum(sheet => sheet.ExpectedFeeCents),
            sheets.Sum(sheet => sheet.PaidFeeCents),
            flatFeeDeductedCents,
            sheets.Sum(sheet => sheet.RemainingFeeCents) - flatFeeDeductedCents,
            sheets.Sum(sheet => sheet.AssociationMarginCents),
            sales.Sum(sale => sale.CheckoutFeeCents) - refundedLines.Sum(line => line.RefundCheckoutFeeCents ?? 0));

        var clients = new SaleStatisticsClientMoney(
            sales.Count,
            sales.Sum(sale => sale.TotalPaidCents),
            refundedLines.Sum(line => line.RefundTotalCents ?? 0),
            SumByPaymentMethod(sales, SalePaymentMethods.Card),
            SumByPaymentMethod(sales, SalePaymentMethods.Cash),
            SumByPaymentMethod(sales, SalePaymentMethods.Cheque));

        var missingItems = sheets.SelectMany(sheet => sheet.PayableItems).Where(item => item.IsMissing).ToList();
        var sellers = new SaleStatisticsSellerPayouts(
            sheets.Sum(sheet => sheet.TotalPayableCents),
            sheets.Count(sheet => sheet.TotalPayableCents > 0),
            missingItems.Count,
            missingItems.Sum(item => item.PriceCents));

        var itemCounts = new SaleStatisticsItemCounts(
            items.Count,
            items.Count(item => item.Status == "registered"),
            items.Count(item => item.Status == "in_stock"),
            items.Count(item => item.Status == "sold"),
            items.Count(item => item.Status == "missing"),
            items.Count(item => item.Status == "defective"));

        var soldPrices = sheets
            .SelectMany(sheet => sheet.PayableItems)
            .Where(item => !item.IsMissing)
            .Select(item => (int?)item.PriceCents)
            .ToList();

        return new SaleStatistics(
            fees,
            clients,
            sellers,
            itemCounts,
            BuildPriceBrackets(items.Select(item => item.SellerPriceCents).ToList(), soldPrices),
            BuildCities(sales.Select(sale => sale.BuyerCity)));
    }

    private static int SumByPaymentMethod(IEnumerable<Data.Entities.SaleEntity> sales, string paymentMethod)
    {
        return sales.Where(sale => sale.PaymentMethod == paymentMethod).Sum(sale => sale.TotalPaidCents);
    }

    private static List<SaleStatisticsPriceBracket> BuildPriceBrackets(
        IReadOnlyCollection<int?> itemPrices,
        IReadOnlyCollection<int?> soldPrices)
    {
        string LabelOf(int? priceCents)
        {
            if (priceCents is not > 0)
            {
                return InvalidPriceLabel;
            }

            return PriceBrackets.First(bracket => priceCents.Value <= bracket.MaxCents).Label;
        }

        SaleStatisticsPriceBracket Build(string label) => new(
            label,
            itemPrices.Count(price => LabelOf(price) == label),
            itemPrices.Where(price => LabelOf(price) == label).Sum(price => price is > 0 ? price.Value : 0),
            soldPrices.Count(price => LabelOf(price) == label),
            soldPrices.Where(price => LabelOf(price) == label).Sum(price => price is > 0 ? price.Value : 0));

        var brackets = PriceBrackets.Select(bracket => Build(bracket.Label)).ToList();

        // 0 EUR is not a valid price but the deposit still accepts it: surface such items
        // rather than hide them, without cluttering the table when there are none.
        var invalid = Build(InvalidPriceLabel);
        if (invalid.ItemCount > 0 || invalid.SoldCount > 0)
        {
            brackets.Add(invalid);
        }

        return brackets;
    }

    // Counted per sale: the same buyer cannot be reliably recognised across sales.
    private static List<SaleStatisticsCity> BuildCities(IEnumerable<string?> buyerCities)
    {
        return buyerCities
            .Select(city => string.IsNullOrWhiteSpace(city) ? MissingCityLabel : city.Trim())
            .GroupBy(city => city.ToLower(FrenchCulture))
            .Select(group => new SaleStatisticsCity(
                // Show the most used spelling, the earliest one on a tie.
                group.GroupBy(spelling => spelling)
                    .OrderByDescending(spelling => spelling.Count())
                    .First()
                    .Key,
                group.Count()))
            .OrderByDescending(city => city.SaleCount)
            .ThenBy(city => city.City, StringComparer.Create(FrenchCulture, ignoreCase: true))
            .ToList();
    }
}
