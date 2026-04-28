using AssociationSaleTool.Web.Controllers.Models;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AssociationSaleTool.Web.Controllers;

[ApiController]
[Route("api/caisse")]
public sealed class CheckoutValidationController(CheckoutService checkoutService) : ControllerBase
{
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
