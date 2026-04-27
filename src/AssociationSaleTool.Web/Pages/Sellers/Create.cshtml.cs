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

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var seller = await intakeService.CreateSellerAsync(
            new CreateSellerRequest(Input.FirstName, Input.LastName, Input.Email, Input.Phone, Input.Notes),
            cancellationToken);

        return RedirectToPage("/Sellers/Details", new { id = seller.Id });
    }

    public sealed class InputModel
    {
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Notes { get; set; }
    }
}
