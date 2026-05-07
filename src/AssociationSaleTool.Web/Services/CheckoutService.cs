using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Pricing;
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Services;

public sealed class CheckoutService
{
    private const string PostSaleMessage = "La vente est en après-vente. Aucune nouvelle vente ne peut être enregistrée.";
    private const string MissingBuyerDetailsMessage = "Le nom, le code postal et la ville de l'acheteur sont obligatoires.";
    private const string MissingInvoiceAddressMessage = "L'adresse de l'acheteur est obligatoire pour imprimer une facture.";
    private readonly AppDbContext _dbContext;
    private readonly SalePhaseService _salePhaseService;
    private readonly PrintingOptions _printingOptions;

    public CheckoutService(
        AppDbContext dbContext,
        SalePhaseService? salePhaseService = null,
        IOptions<PrintingOptions>? printingOptions = null)
    {
        _dbContext = dbContext;
        _salePhaseService = salePhaseService ?? new SalePhaseService(dbContext);
        _printingOptions = printingOptions?.Value ?? new PrintingOptions();
    }

    public async Task<List<CheckoutCatalogItem>> ListCatalogItemsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Items
            .AsNoTracking()
            .Where(x => x.Status == "in_stock")
            .OrderBy(x => x.Reference)
            .Select(x => new CheckoutCatalogItem(
                x.Id,
                x.Reference,
                x.Title,
                x.SellerPriceCents))
            .ToListAsync(cancellationToken);
    }

    public async Task<CheckoutValidationResult> ValidateCartAsync(
        IReadOnlyList<long> itemIds,
        CancellationToken cancellationToken = default)
    {
        if (!await _salePhaseService.CanCreateSalesAsync(cancellationToken))
        {
            return new CheckoutValidationResult(false, [], null, 0, PostSaleMessage);
        }

        var items = await LoadCartItemsAsync(itemIds, asTracking: false, cancellationToken);
        return BuildValidationResult(itemIds, items);
    }

    public async Task<CheckoutSaleResult> SaveSaleAsync(
        IReadOnlyList<long> itemIds,
        string? buyerAddress = null,
        string? buyerName = null,
        string? buyerPostalCode = null,
        string? buyerCity = null,
        CancellationToken cancellationToken = default,
        CheckoutPaymentDetails? paymentDetails = null,
        bool createInvoice = false)
    {
        var normalizedBuyerDetails = NormalizeBuyerDetails(buyerName, buyerPostalCode, buyerCity);
        if (!normalizedBuyerDetails.IsValid)
        {
            return new CheckoutSaleResult(
                false,
                null,
                [],
                null,
                0,
                normalizedBuyerDetails.Message,
                IsValidationError: true);
        }

        var normalizedBuyerAddress = NormalizeOptionalText(buyerAddress);
        if (createInvoice && normalizedBuyerAddress is null)
        {
            return new CheckoutSaleResult(
                false,
                null,
                [],
                null,
                0,
                MissingInvoiceAddressMessage,
                IsValidationError: true);
        }

        var normalizedPayment = NormalizePaymentDetails(paymentDetails);
        if (!normalizedPayment.IsValid)
        {
            return new CheckoutSaleResult(
                false,
                null,
                [],
                null,
                0,
                normalizedPayment.Message,
                IsValidationError: true);
        }

        if (!await _salePhaseService.CanCreateSalesAsync(cancellationToken))
        {
            return new CheckoutSaleResult(false, null, [], null, 0, PostSaleMessage);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var items = await LoadCartItemsAsync(itemIds, asTracking: true, cancellationToken);
        var validation = BuildValidationResult(itemIds, items);

        if (!validation.IsValid || validation.Totals is null)
        {
            return new CheckoutSaleResult(
                false,
                null,
                validation.ConflictItemIds,
                null,
                validation.ItemCount);
        }

        var soldAt = DateTime.UtcNow;
        var sale = new SaleEntity
        {
            SoldAt = soldAt,
            SubtotalSellerPriceCents = validation.Totals.SubtotalSellerPriceCents,
            CheckoutFeeCents = validation.Totals.CheckoutFeeCents,
            TotalPaidCents = validation.Totals.TotalPaidCents,
            BuyerAddress = normalizedBuyerAddress,
            BuyerName = normalizedBuyerDetails.BuyerName,
            BuyerPostalCode = normalizedBuyerDetails.BuyerPostalCode,
            BuyerCity = normalizedBuyerDetails.BuyerCity,
            PaymentMethod = normalizedPayment.PaymentMethod,
            ChequeNumber = normalizedPayment.ChequeNumber,
            ChequeBankName = normalizedPayment.ChequeBankName
        };

        if (createInvoice)
        {
            sale.InvoiceIssuedAt = soldAt;
            sale.InvoiceNumber = await CreateNextInvoiceNumberAsync(cancellationToken);
        }

        foreach (var item in items)
        {
            item.Status = "sold";
            sale.SaleLines.Add(new SaleLineEntity
            {
                ItemId = item.Id,
                SellerPriceAtSaleCents = item.SellerPriceCents
            });
        }

        _dbContext.Sales.Add(sale);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CheckoutSaleResult(
            true,
            sale.Id,
            [],
            validation.Totals,
            validation.ItemCount,
            InvoiceNumber: sale.InvoiceNumber,
            InvoiceIssuedAt: sale.InvoiceIssuedAt);
    }

    private async Task<string> CreateNextInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var prefix = _printingOptions.InvoiceNumberPrefix;
        var lastInvoiceNumber = await _dbContext.Sales
            .AsNoTracking()
            .Where(x => x.InvoiceNumber != null && x.InvoiceNumber.StartsWith(prefix))
            .Select(x => x.InvoiceNumber!)
            .OrderByDescending(x => x)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;
        if (lastInvoiceNumber is not null
            && lastInvoiceNumber.Length > prefix.Length
            && int.TryParse(lastInvoiceNumber[prefix.Length..], out var lastSequence))
        {
            nextSequence = lastSequence + 1;
        }

        return $"{prefix}{nextSequence:0000}";
    }

    private static BuyerDetailsNormalizationResult NormalizeBuyerDetails(
        string? buyerName,
        string? buyerPostalCode,
        string? buyerCity)
    {
        var normalizedBuyerName = NormalizeOptionalText(buyerName);
        var normalizedPostalCode = NormalizeOptionalText(buyerPostalCode);
        var normalizedCity = NormalizeOptionalText(buyerCity);

        if (normalizedBuyerName is null || normalizedPostalCode is null || normalizedCity is null)
        {
            return BuyerDetailsNormalizationResult.Invalid(MissingBuyerDetailsMessage);
        }

        return BuyerDetailsNormalizationResult.Valid(normalizedBuyerName, normalizedPostalCode, normalizedCity);
    }

    private static PaymentNormalizationResult NormalizePaymentDetails(CheckoutPaymentDetails? paymentDetails)
    {
        if (paymentDetails is null)
        {
            return PaymentNormalizationResult.Valid(SalePaymentMethods.Card, null, null);
        }

        var paymentMethod = SalePaymentMethods.Normalize(paymentDetails.PaymentMethod);
        if (paymentMethod is null)
        {
            return PaymentNormalizationResult.Invalid("Le mode de paiement est invalide.");
        }

        if (paymentMethod != SalePaymentMethods.Cheque)
        {
            return PaymentNormalizationResult.Valid(paymentMethod, null, null);
        }

        var chequeNumber = NormalizeOptionalText(paymentDetails.ChequeNumber);
        var chequeBankName = NormalizeOptionalText(paymentDetails.ChequeBankName);
        if (chequeNumber is null || chequeBankName is null)
        {
            return PaymentNormalizationResult.Invalid("Le numéro de chèque et le nom de la banque sont obligatoires pour un paiement par chèque.");
        }

        return PaymentNormalizationResult.Valid(paymentMethod, chequeNumber, chequeBankName);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task<List<ItemEntity>> LoadCartItemsAsync(
        IReadOnlyList<long> itemIds,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var distinctIds = itemIds.Distinct().ToList();
        var query = _dbContext.Items.Where(x => distinctIds.Contains(x.Id));

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .OrderBy(x => x.Reference)
            .ToListAsync(cancellationToken);
    }

    private static CheckoutValidationResult BuildValidationResult(
        IReadOnlyList<long> requestedItemIds,
        List<ItemEntity> loadedItems)
    {
        var loadedById = loadedItems.ToDictionary(x => x.Id);
        var conflictItemIds = requestedItemIds
            .Where(itemId => !loadedById.TryGetValue(itemId, out var item) || item.Status != "in_stock")
            .Distinct()
            .ToList();

        if (requestedItemIds.Count == 0 || conflictItemIds.Count > 0)
        {
            return new CheckoutValidationResult(false, conflictItemIds, null, loadedItems.Count);
        }

        var subtotalCents = loadedItems.Sum(x => x.SellerPriceCents);
        var totals = CheckoutPricingCalculator.CalculateTotals(subtotalCents);
        var cartTotals = new CheckoutCartTotals(
            totals.SubtotalSellerPriceCents,
            totals.CheckoutFeeCents,
            totals.TotalPaidCents);

        return new CheckoutValidationResult(true, [], cartTotals, loadedItems.Count);
    }

    private sealed record PaymentNormalizationResult(
        bool IsValid,
        string? PaymentMethod,
        string? ChequeNumber,
        string? ChequeBankName,
        string? Message = null)
    {
        public static PaymentNormalizationResult Valid(
            string paymentMethod,
            string? chequeNumber,
            string? chequeBankName)
        {
            return new PaymentNormalizationResult(true, paymentMethod, chequeNumber, chequeBankName);
        }

        public static PaymentNormalizationResult Invalid(string message)
        {
            return new PaymentNormalizationResult(false, null, null, null, message);
        }
    }

    private sealed record BuyerDetailsNormalizationResult(
        bool IsValid,
        string? BuyerName,
        string? BuyerPostalCode,
        string? BuyerCity,
        string? Message = null)
    {
        public static BuyerDetailsNormalizationResult Valid(
            string buyerName,
            string buyerPostalCode,
            string buyerCity)
        {
            return new BuyerDetailsNormalizationResult(true, buyerName, buyerPostalCode, buyerCity);
        }

        public static BuyerDetailsNormalizationResult Invalid(string message)
        {
            return new BuyerDetailsNormalizationResult(false, null, null, null, message);
        }
    }
}
