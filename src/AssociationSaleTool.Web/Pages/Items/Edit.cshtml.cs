using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace AssociationSaleTool.Web.Pages.Items;

public sealed class EditModel(IntakeService intakeService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public ItemEntity? Item { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var item = await intakeService.GetItemAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        Item = item;
        Input = new InputModel
        {
            Title = item.Title,
            SellerPriceCents = item.SellerPriceCents
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id, CancellationToken cancellationToken)
    {
        var item = await intakeService.GetItemAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        Item = item;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await intakeService.UpdateItemAsync(id, new UpdateItemRequest(Input.Title, Input.SellerPriceCents), cancellationToken);
        return RedirectToPage("/Sellers/Details", new { id = item.SellerId });
    }

    public sealed class InputModel
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int SellerPriceCents { get; set; }
    }
}
