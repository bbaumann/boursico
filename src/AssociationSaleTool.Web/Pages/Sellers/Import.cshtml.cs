using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class ImportModel(SellerFormImportService importService, IntakeService intakeService) : PageModel
{
    [BindProperty]
    public IFormFile? FormFile { get; set; }

    [BindProperty]
    public DuplicateAppendInputModel DuplicateInput { get; set; } = new();

    public bool CanImportSellerData { get; private set; }

    public SellerFormImportResult? ImportResult { get; private set; }

    public SellerFormDuplicateAppendResult? AppendResult { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        CanImportSellerData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync(CancellationToken cancellationToken)
    {
        CanImportSellerData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
        if (!CanImportSellerData)
        {
            ModelState.AddModelError(string.Empty, "Les soumissions ne peuvent pas être importées dans le statut actuel de la vente.");
            return Page();
        }

        ImportResult = await importService.ImportAsync(FormFile, cancellationToken);
        foreach (var error in ImportResult.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAppendDuplicateAsync(CancellationToken cancellationToken)
    {
        CanImportSellerData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
        if (!CanImportSellerData)
        {
            ModelState.AddModelError(string.Empty, "Les articles ne peuvent pas être ajoutés dans le statut actuel de la vente.");
            return Page();
        }

        var rows = DuplicateInput.Rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Title))
            .Select(row => new CreateItemRowRequest(row.Title.Trim(), row.SellerPriceCents))
            .ToList();
        AppendResult = await importService.AppendDuplicateItemsAsync(
            DuplicateInput.SellerId,
            rows,
            DuplicateInput.ImportNotes,
            cancellationToken);

        if (AppendResult.Outcome == SellerFormDuplicateAppendOutcome.NotFound)
        {
            return NotFound();
        }

        if (AppendResult.Outcome == SellerFormDuplicateAppendOutcome.NotAllowed)
        {
            ModelState.AddModelError(string.Empty, AppendResult.Message ?? "Les articles ne peuvent pas être ajoutés dans le statut actuel de la vente.");
        }

        return Page();
    }

    public static string FormatOutcome(SellerFormImportRowOutcome outcome)
    {
        return outcome switch
        {
            SellerFormImportRowOutcome.Imported => "Importé",
            SellerFormImportRowOutcome.Duplicate => "Doublon",
            SellerFormImportRowOutcome.SellerError => "Erreur vendeur",
            SellerFormImportRowOutcome.ImportError => "Erreur d'import",
            _ => outcome.ToString()
        };
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{priceCents / 100} €";
    }

    public sealed class DuplicateAppendInputModel
    {
        public long SellerId { get; set; }

        public string? ImportNotes { get; set; }

        public List<DuplicateAppendItemInputModel> Rows { get; set; } = [];
    }

    public sealed class DuplicateAppendItemInputModel
    {
        public string Title { get; set; } = string.Empty;

        public int SellerPriceCents { get; set; }
    }
}
