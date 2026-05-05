using AssociationSaleTool.Web.Controllers.Models;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
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

        var result = await checkoutService.SaveSaleAsync(
            request.ItemIds,
            request.BuyerAddress,
            request.BuyerName,
            request.BuyerPostalCode,
            request.BuyerCity,
            cancellationToken,
            new CheckoutPaymentDetails(request.PaymentMethod, request.ChequeNumber, request.ChequeBankName));

        if (result.Saved)
        {
            return Created($"/api/ventes/{result.SaleId}", result);
        }

        return result.IsValidationError
            ? BadRequest(result)
            : Conflict(result);
    }
}
