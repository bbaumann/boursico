using AssociationSaleTool.Web.Controllers.Models;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AssociationSaleTool.Web.Controllers;

[ApiController]
[Route("api/caisse")]
public sealed class CheckoutValidationController(CheckoutService checkoutService) : ControllerBase
{
    // Lets the checkout reload its catalog without reloading the page, so items put back in
    // stock or repriced during the sale show up without losing the sale being typed.
    [HttpGet("articles")]
    public async Task<IActionResult> ListCatalogItemsAsync(CancellationToken cancellationToken)
    {
        var catalog = await checkoutService.ListCatalogItemsAsync(cancellationToken);
        return Ok(catalog);
    }

    [HttpPost("validation")]
    public async Task<IActionResult> ValidateCartAsync(
        CheckoutCartRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ItemIds.Count == 0)
        {
            return BadRequest(new { message = "Le panier est vide." });
        }

        var result = await checkoutService.ValidateCartAsync(request.ItemIds, cancellationToken);
        return result.IsValid ? Ok(result) : Conflict(result);
    }
}
