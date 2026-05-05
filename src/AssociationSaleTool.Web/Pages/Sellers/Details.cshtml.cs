using System.Globalization;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Sellers;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class DetailsModel(IntakeService intakeService) : PageModel
{
    private const int DefaultRowCount = 10;
    private const string ExcelDuplicateAlertMessage = "Ce vendeur existe déjà. Les articles du fichier Excel ont été ajoutés au formulaire ci-dessous. Vérifiez puis enregistrez le lot.";

    [BindProperty]
    public ItemBatchInput Input { get; set; } = CreateDefaultInput();

    public SellerEntity? Seller { get; private set; }

    public IReadOnlyList<ItemEntity> Items { get; private set; } = [];

    public bool CanEditItemMasterData { get; private set; }

    public bool CanEditSellerMasterData { get; private set; }

    public SellerFeeStatus FeeStatus { get; private set; } = SellerFeeStatus.Empty;

    public string? PendingRowsAlert { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        CanEditItemMasterData = await intakeService.CanEditItemMasterDataAsync(cancellationToken);
        CanEditSellerMasterData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
        Input = CreateDefaultInput();
        ApplyPendingRows(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        CanEditItemMasterData = await intakeService.CanEditItemMasterDataAsync(cancellationToken);
        CanEditSellerMasterData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);

        if (!CanEditItemMasterData)
        {
            EnsureDefaultRows();
            ModelState.AddModelError(string.Empty, "Les articles ne peuvent pas être ajoutés dans le statut actuel de la vente.");
            return Page();
        }

        var rowsToCreate = ValidateRows();
        EnsureDefaultRows();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await intakeService.CreateItemBatchAsync(
            new CreateItemBatchRequest(id, Input.IntakeMode, rowsToCreate),
            cancellationToken);

        return RedirectToPage("/Sellers/Details", new { id });
    }

    public async Task<IActionResult> OnPostDeleteItemAsync(long id, long itemId, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        Input = CreateDefaultInput();
        CanEditItemMasterData = await intakeService.CanEditItemMasterDataAsync(cancellationToken);
        CanEditSellerMasterData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);

        if (!CanEditItemMasterData)
        {
            ModelState.AddModelError(string.Empty, "Les articles ne peuvent pas être supprimés dans le statut actuel de la vente.");
            return Page();
        }

        var result = await intakeService.DeleteSellerItemAsync(id, itemId, cancellationToken);
        if (result is DeleteItemResult.NotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Seuls les articles enregistrés ou en stock peuvent être supprimés.");
            return Page();
        }

        return RedirectToPage("/Sellers/Details", new { id });
    }

    public async Task<IActionResult> OnPostMarkItemInStockAsync(long id, long itemId, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        Input = CreateDefaultInput();
        CanEditItemMasterData = await intakeService.CanEditItemMasterDataAsync(cancellationToken);
        CanEditSellerMasterData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);

        if (!CanEditItemMasterData)
        {
            ModelState.AddModelError(string.Empty, "Les articles ne peuvent pas changer de statut dans le statut actuel de la vente.");
            return Page();
        }

        var result = await intakeService.MarkSellerItemInStockAsync(id, itemId, cancellationToken);
        if (result is ItemStatusChangeResult.NotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Seuls les articles enregistrés peuvent être marqués en stock.");
            return Page();
        }

        return RedirectToPage("/Sellers/Details", new { id });
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{(priceCents / 100m).ToString("0.00", CultureInfo.InvariantCulture)} EUR";
    }

    private void PopulateSellerState(SellerEntity seller)
    {
        Seller = seller;
        Items = seller.Items.OrderBy(x => x.ItemNumber).ToList();
        var expectedFeeCents = SellerFeeCalculator.CalculateExpectedFeeCents(seller.Items.Count);
        var remainingFeeCents = SellerFeeCalculator.CalculateRemainingFeeCents(expectedFeeCents, seller.FeePaidCents);
        FeeStatus = new SellerFeeStatus(
            expectedFeeCents,
            seller.FeePaidCents,
            remainingFeeCents,
            remainingFeeCents == 0);
    }

    private List<CreateItemRowRequest> ValidateRows()
    {
        var rowsToCreate = new List<CreateItemRowRequest>();

        for (var index = 0; index < Input.Rows.Count; index++)
        {
            var row = Input.Rows[index];
            var titleMissing = string.IsNullOrWhiteSpace(row.Title);
            var priceMissing = string.IsNullOrWhiteSpace(row.SellerPrice);

            if (titleMissing && priceMissing)
            {
                continue;
            }

            if (titleMissing)
            {
                ModelState.AddModelError($"Input.Rows[{index}].Title", "Le titre est obligatoire lorsqu'un prix est saisi.");
                continue;
            }

            if (priceMissing)
            {
                ModelState.AddModelError($"Input.Rows[{index}].SellerPrice", "Le prix vendeur est obligatoire lorsqu'un titre est saisi.");
                continue;
            }

            if (!EuroPriceInput.TryParseToCents(row.SellerPrice, out var sellerPriceCents))
            {
                ModelState.AddModelError($"Input.Rows[{index}].SellerPrice", "Le prix vendeur doit être un montant entier en euros.");
                continue;
            }

            if (sellerPriceCents < 0)
            {
                ModelState.AddModelError($"Input.Rows[{index}].SellerPrice", "Le prix vendeur doit être supérieur ou égal à zéro.");
                continue;
            }

            rowsToCreate.Add(new CreateItemRowRequest(row.Title!, sellerPriceCents));
        }

        if (rowsToCreate.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Saisissez au moins une ligne d'article complète.");
        }

        return rowsToCreate;
    }

    private void EnsureDefaultRows()
    {
        while (Input.Rows.Count < DefaultRowCount)
        {
            Input.Rows.Add(new ItemRowInput());
        }
    }

    private void ApplyPendingRows(long sellerId)
    {
        PendingRowsAlert = null;
        var httpContext = PageContext?.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var session = httpContext.Features.Get<ISessionFeature>()?.Session;
        if (session is null)
        {
            return;
        }

        var pendingRows = PendingSellerItemRowsSession.Consume(session, sellerId);
        if (pendingRows is null)
        {
            return;
        }

        Input.IntakeMode = pendingRows.IntakeMode;
        Input.Rows = pendingRows.Rows
            .Select(row => new ItemRowInput
            {
                Title = row.Title,
                SellerPrice = EuroPriceInput.FormatFromCents(row.SellerPriceCents)
            })
            .ToList();
        EnsureDefaultRows();
        PendingRowsAlert = pendingRows.AlertMessageKey == PendingSellerItemRowsSession.ExcelDuplicateAlertKey
            ? ExcelDuplicateAlertMessage
            : ExcelDuplicateAlertMessage;
    }

    private static ItemBatchInput CreateDefaultInput()
    {
        return new ItemBatchInput
        {
            IntakeMode = IntakeMode.EmailPreregistration,
            Rows = Enumerable.Range(0, DefaultRowCount)
                .Select(_ => new ItemRowInput())
                .ToList()
        };
    }

    public sealed class ItemBatchInput
    {
        public IntakeMode IntakeMode { get; set; }

        public List<ItemRowInput> Rows { get; set; } = [];
    }

    public sealed class ItemRowInput
    {
        public string? Title { get; set; }

        public string? SellerPrice { get; set; }
    }

    public sealed record SellerFeeStatus(
        int ExpectedFeeCents,
        int PaidFeeCents,
        int RemainingFeeCents,
        bool IsFullyPaid)
    {
        public static SellerFeeStatus Empty { get; } = new(0, 0, 0, true);
    }
}
