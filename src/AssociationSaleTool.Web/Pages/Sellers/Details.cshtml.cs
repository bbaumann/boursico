using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class DetailsModel(IntakeService intakeService) : PageModel
{
    private const int DefaultRowCount = 10;

    [BindProperty]
    public ItemBatchInput Input { get; set; } = CreateDefaultInput();

    public SellerEntity? Seller { get; private set; }

    public IReadOnlyList<ItemEntity> Items { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        Seller = loaded;
        Items = loaded.Items.OrderBy(x => x.ItemNumber).ToList();
        Input = CreateDefaultInput();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        Seller = loaded;
        Items = loaded.Items.OrderBy(x => x.ItemNumber).ToList();

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

        Seller = loaded;
        Items = loaded.Items.OrderBy(x => x.ItemNumber).ToList();
        Input = CreateDefaultInput();

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

        Seller = loaded;
        Items = loaded.Items.OrderBy(x => x.ItemNumber).ToList();
        Input = CreateDefaultInput();

        var result = await intakeService.MarkSellerItemInStockAsync(id, itemId, cancellationToken);
        if (result is ItemStatusChangeResult.NotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Seuls les articles enregistrés peuvent être marqués en stock.");
            return Page();
        }

        return RedirectToPage("/Sellers/Details", new { id });
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
                ModelState.AddModelError($"Input.Rows[{index}].SellerPrice", "Le prix vendeur doit être un montant décimal en euros.");
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
}
