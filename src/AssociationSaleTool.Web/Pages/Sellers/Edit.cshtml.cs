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

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var seller = await intakeService.GetSellerAsync(id, cancellationToken);
        if (seller is null)
        {
            return NotFound();
        }

        SellerId = seller.Id;
        Input = new InputModel
        {
            FirstName = seller.FirstName,
            LastName = seller.LastName,
            Email = seller.Email,
            Phone = seller.Phone,
            Notes = seller.Notes
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id, CancellationToken cancellationToken)
    {
        SellerId = id;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await intakeService.UpdateSellerAsync(
            id,
            new UpdateSellerRequest(Input.FirstName, Input.LastName, Input.Email, Input.Phone, Input.Notes),
            cancellationToken);

        return RedirectToPage("/Sellers/Details", new { id });
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Le prénom est obligatoire.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le nom est obligatoire.")]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "L'adresse e-mail doit être valide.")]
        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Notes { get; set; }
    }
}
