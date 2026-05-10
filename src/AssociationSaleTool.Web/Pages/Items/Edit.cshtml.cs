using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Sellers;
using AssociationSaleTool.Web.Infrastructure;
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

    public bool CanEditItem { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var item = await intakeService.GetItemAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        Item = item;
        CanEditItem = await intakeService.CanEditItemMasterDataAsync(cancellationToken)
            && item.Seller.DepositStatus == SellerDepositStatuses.Pending
            && item.Status is "registered" or "in_stock";
        Input = new InputModel
        {
            Title = item.Title,
            SellerPrice = EuroPriceInput.FormatFromCents(item.SellerPriceCents)
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
        CanEditItem = await intakeService.CanEditItemMasterDataAsync(cancellationToken)
            && item.Seller.DepositStatus == SellerDepositStatuses.Pending
            && item.Status is "registered" or "in_stock";

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!EuroPriceInput.TryParseToCents(Input.SellerPrice, out var sellerPriceCents))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.SellerPrice)}", "Le prix vendeur doit être un montant entier en euros.");
            return Page();
        }

        if (sellerPriceCents < 0)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.SellerPrice)}", "Le prix vendeur doit être supérieur ou égal à zéro.");
            return Page();
        }

        var result = await intakeService.UpdateItemAsync(id, new UpdateItemRequest(Input.Title, sellerPriceCents), cancellationToken);
        if (result == UpdateItemResult.NotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Cet article ne peut pas être modifié dans le statut actuel de la vente.");
            CanEditItem = false;
            return Page();
        }

        return RedirectToPage("/Sellers/Details", new { id = item.SellerId });
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Le titre est obligatoire.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le prix vendeur est obligatoire.")]
        public string SellerPrice { get; set; } = string.Empty;
    }
}
