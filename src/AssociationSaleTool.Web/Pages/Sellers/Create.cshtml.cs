using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class CreateModel(IntakeService intakeService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool CanEditSellerMasterData { get; private set; }

    public IReadOnlyList<SellerDuplicateMatch> DuplicateMatches { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CanEditSellerMasterData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
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

        var feePaidCents = ParseFeePaid();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await intakeService.CreateSellerAsync(
            new CreateSellerRequest(
                Input.FirstName,
                Input.LastName,
                Input.Email,
                Input.Phone,
                Input.PostalAddress,
                Input.Notes,
                feePaidCents,
                Input.PostalCode,
                Input.City),
            cancellationToken);

        if (result.Outcome == CreateSellerOutcome.DuplicateFound)
        {
            DuplicateMatches = result.DuplicateMatches;
            ModelState.AddModelError(string.Empty, BuildDuplicateMessage(DuplicateMatches));
            return Page();
        }

        return RedirectToPage("/Sellers/Details", new { id = result.Seller!.Id });
    }

    private static string BuildDuplicateMessage(IReadOnlyList<SellerDuplicateMatch> matches)
    {
        return "Un vendeur existe déjà avec les mêmes nom, prénom et téléphone. Ouvrez le vendeur existant avant de continuer.";
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

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Le prénom est obligatoire.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le nom est obligatoire.")]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "L'adresse e-mail doit être valide.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Le téléphone est obligatoire.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "L'adresse postale est obligatoire.")]
        public string PostalAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le code postal est obligatoire.")]
        public string PostalCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "La ville est obligatoire.")]
        public string City { get; set; } = string.Empty;

        public string? FeePaid { get; set; } = "0";

        public string? Notes { get; set; }
    }
}
