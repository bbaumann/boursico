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

    // The import summary lives only in the page: the browser posts each merge in the
    // background and asks for JSON, so the summary stays on screen. A plain form post
    // (no JavaScript) still gets the page back.
    public async Task<IActionResult> OnPostAppendDuplicateAsync(CancellationToken cancellationToken)
    {
        var wantsJson = Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
        CanImportSellerData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
        if (!CanImportSellerData)
        {
            const string notAllowedMessage = "Les articles ne peuvent pas être ajoutés dans le statut actuel de la vente.";
            if (wantsJson)
            {
                return new JsonResult(new { outcome = "notAllowed", message = notAllowedMessage });
            }

            ModelState.AddModelError(string.Empty, notAllowedMessage);
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

        if (wantsJson)
        {
            return AppendResult.Outcome switch
            {
                SellerFormDuplicateAppendOutcome.Appended => new JsonResult(new
                {
                    outcome = "appended",
                    message = FormatAppendSummary(AppendResult),
                    sellerUrl = Url.Page("/Sellers/Details", new { id = AppendResult.Seller!.Id })
                }),
                SellerFormDuplicateAppendOutcome.NotFound => new JsonResult(new { outcome = "notFound", message = AppendResult.Message }),
                _ => new JsonResult(new { outcome = "notAllowed", message = AppendResult.Message })
            };
        }

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

    public static string FormatAppendSummary(SellerFormDuplicateAppendResult result)
    {
        var created = result.CreatedItemCount;
        var ignored = result.IgnoredExistingItemCount;
        return $"{created} article{Plural(created)} ajouté{Plural(created)}, "
            + $"{ignored} article{Plural(ignored)} déjà présent{Plural(ignored)} ignoré{Plural(ignored)}.";

        static string Plural(int count) => count > 1 ? "s" : string.Empty;
    }

    public static string FormatPrice(int? priceCents)
    {
        return priceCents is null ? "À compléter" : $"{priceCents / 100} €";
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

        public int? SellerPriceCents { get; set; }
    }
}
