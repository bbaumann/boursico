using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class CreateModel(IntakeService intakeService) : PageModel
{
    private const int DefaultRowCount = 10;

    [BindProperty]
    public InputModel Input { get; set; } = CreateDefaultInput();

    public bool CanEditSellerMasterData { get; private set; }

    public IReadOnlyList<SellerDuplicateMatch> DuplicateMatches { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CanEditSellerMasterData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
        EnsureDefaultRows();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        CanEditSellerMasterData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
        if (!CanEditSellerMasterData)
        {
            ModelState.AddModelError(string.Empty, "Les vendeurs ne peuvent pas être créés dans le statut actuel de la vente.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Input.IntakeMode = IntakeMode.EmailPreregistration;
        var feePaidCents = ParseFeePaid();
        var rowsToCreate = ValidateRows();
        EnsureDefaultRows();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        CreateSellerWithItemsResult result;
        try
        {
            result = await intakeService.CreateSellerWithItemsAsync(
                new CreateSellerWithItemsRequest(
                    Input.FirstName,
                    Input.LastName,
                    Input.Email,
                    Input.Phone,
                    Input.PostalAddress,
                    Input.Notes,
                    IntakeMode.EmailPreregistration,
                    rowsToCreate,
                    feePaidCents,
                    Input.PostalCode,
                    Input.City,
                    IdentityDocumentType: Input.IdentityDocumentType,
                    IdentityDocumentNumber: Input.IdentityDocumentNumber,
                    IdentityDocumentIssuedOn: Input.IdentityDocumentIssuedOn,
                    IdentityDocumentIssuer: Input.IdentityDocumentIssuer),
                cancellationToken);
        }
        catch (InvalidSellerPostalCityException exception)
        {
            ModelState.AddModelError("Input.City", exception.Message);
            return Page();
        }

        if (result.Outcome == CreateSellerWithItemsOutcome.ExistingSellerFound)
        {
            DuplicateMatches = result.DuplicateMatches;
            if (TryStorePendingRowsAndRedirect(DuplicateMatches, IntakeMode.EmailPreregistration, rowsToCreate, out var redirect))
            {
                return redirect;
            }

            ModelState.AddModelError(string.Empty, BuildDuplicateMessage(DuplicateMatches));
            return Page();
        }

        return RedirectToPage("/Sellers/Details", new { id = result.Seller!.Id });
    }

    private static string BuildDuplicateMessage(IReadOnlyList<SellerDuplicateMatch> matches)
    {
        if (matches.Any(match => match.Reason == SellerDuplicateMatchReason.NameOnlySubmittedPhoneMissing))
        {
            return "Un vendeur existe déjà avec les mêmes nom et prénom. Aucun téléphone n'a été saisi : ouvrez le vendeur existant ou saisissez un téléphone distinct avant de continuer.";
        }

        return "Un vendeur existe déjà avec les mêmes nom, prénom et téléphone. Ouvrez le vendeur existant avant de continuer.";
    }

    private bool TryStorePendingRowsAndRedirect(
        IReadOnlyList<SellerDuplicateMatch> matches,
        IntakeMode intakeMode,
        IReadOnlyList<CreateItemRowRequest> rows,
        out IActionResult redirect)
    {
        redirect = Page();
        if (matches.Count != 1)
        {
            return false;
        }

        var match = matches[0];
        PendingSellerItemRowsSession.Store(
            HttpContext.Session,
            new PendingSellerItemRows(
                match.SellerId,
                intakeMode,
                rows,
                PendingSellerItemRowsSession.ExcelDuplicateAlertKey));
        redirect = RedirectToPage("/Sellers/Details", new { id = match.SellerId });
        return true;
    }

    private int ParseFeePaid()
    {
        if (string.IsNullOrWhiteSpace(Input.FeePaid))
        {
            return 0;
        }

        if (!EuroPriceInput.TryParseToCents(Input.FeePaid, out var feePaidCents))
        {
            ModelState.AddModelError("Input.FeePaid", "Les frais payés doivent être un montant entier en euros.");
            return 0;
        }

        if (feePaidCents < 0)
        {
            ModelState.AddModelError("Input.FeePaid", "Les frais payés doivent être supérieurs ou égaux à zéro.");
            return 0;
        }

        return feePaidCents;
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

    private static InputModel CreateDefaultInput()
    {
        return new InputModel
        {
            IntakeMode = IntakeMode.EmailPreregistration,
            Rows = Enumerable.Range(0, DefaultRowCount)
                .Select(_ => new ItemRowInput())
                .ToList()
        };
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Le prénom est obligatoire.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le nom est obligatoire.")]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "L'adresse e-mail doit être valide.")]
        public string? Email { get; set; }

        public string Phone { get; set; } = string.Empty;

        public string PostalAddress { get; set; } = string.Empty;

        public string PostalCode { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string? IdentityDocumentType { get; set; }

        public string? IdentityDocumentNumber { get; set; }

        public DateOnly? IdentityDocumentIssuedOn { get; set; }

        public string? IdentityDocumentIssuer { get; set; }

        public string? FeePaid { get; set; } = "0";

        public string? Notes { get; set; }

        public IntakeMode IntakeMode { get; set; }

        public List<ItemRowInput> Rows { get; set; } = [];
    }

    public sealed class ItemRowInput
    {
        public string? Title { get; set; }

        public string? SellerPrice { get; set; }
    }
}
