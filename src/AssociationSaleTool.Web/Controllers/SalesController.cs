using AssociationSaleTool.Web.Controllers.Models;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AssociationSaleTool.Web.Controllers;

[ApiController]
[Route("api/ventes")]
public sealed class SalesController(CheckoutService checkoutService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateSaleAsync(
        CheckoutCartRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ItemIds.Count == 0)
        {
            return BadRequest(new { message = "Le panier est vide." });
        }

        var result = await checkoutService.SaveSaleAsync(request.ItemIds, request.ReceiptDetails, cancellationToken);
        return result.Saved
            ? Created($"/api/ventes/{result.SaleId}", result)
            : Conflict(result);
    }
}
