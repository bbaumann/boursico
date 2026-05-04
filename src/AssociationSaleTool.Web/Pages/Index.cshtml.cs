using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AssociationSaleTool.Web.Services;

namespace AssociationSaleTool.Web.Pages;

public class IndexModel(SalePhaseService salePhaseService) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var phase = await salePhaseService.GetCurrentPhaseAsync(cancellationToken);
        var target = phase switch
        {
            SalePhase.Presale => "/vendeurs",
            SalePhase.Sale => "/caisse",
            SalePhase.PostSale => "/rapprochement",
            _ => "/vendeurs"
        };

        return Redirect(target);
    }
}
