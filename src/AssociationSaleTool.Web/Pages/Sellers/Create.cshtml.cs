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

        var result = await intakeService.CreateSellerAsync(
            new CreateSellerRequest(Input.FirstName, Input.LastName, Input.Email, Input.Phone, Input.PostalAddress, Input.Notes),
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

        public string? Notes { get; set; }
    }
}
