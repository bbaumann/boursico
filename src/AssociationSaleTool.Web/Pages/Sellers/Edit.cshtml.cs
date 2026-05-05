using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class EditModel(IntakeService intakeService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public long SellerId { get; private set; }

    public bool CanEditSellerMasterData { get; private set; }

    public IReadOnlyList<SellerDuplicateMatch> DuplicateMatches { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var seller = await intakeService.GetSellerAsync(id, cancellationToken);
        if (seller is null)
        {
            return NotFound();
        }

        CanEditSellerMasterData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
        SellerId = seller.Id;
        PopulateInput(seller);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id, CancellationToken cancellationToken)
    {
        SellerId = id;
        CanEditSellerMasterData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
        if (!CanEditSellerMasterData)
        {
            var seller = await intakeService.GetSellerAsync(id, cancellationToken);
            if (seller is null)
            {
                return NotFound();
            }

            ModelState.Clear();
            PopulateInput(seller);
            ModelState.AddModelError(string.Empty, "Les informations du vendeur ne peuvent pas être modifiées dans le statut actuel de la vente.");
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

        var result = await intakeService.UpdateSellerAsync(
            id,
            new UpdateSellerRequest(
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

        if (result == UpdateSellerResult.NotFound)
        {
            return NotFound();
        }

        if (result == UpdateSellerResult.NotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Les informations du vendeur ne peuvent pas être modifiées dans le statut actuel de la vente.");
            return Page();
        }

        if (result.Outcome == UpdateSellerOutcome.DuplicateFound)
        {
            DuplicateMatches = result.DuplicateMatches;
            ModelState.AddModelError(string.Empty, BuildDuplicateMessage(DuplicateMatches));
            return Page();
        }

        return RedirectToPage("/Sellers/Details", new { id });
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

    private void PopulateInput(AssociationSaleTool.Web.Data.Entities.SellerEntity seller)
    {
        Input = new InputModel
        {
            FirstName = seller.FirstName,
            LastName = seller.LastName,
            Email = seller.Email,
            Phone = seller.Phone,
            PostalAddress = seller.PostalAddress,
            PostalCode = seller.PostalCode,
            City = seller.City,
            FeePaid = EuroPriceInput.FormatFromCents(seller.FeePaidCents),
            Notes = seller.Notes
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
