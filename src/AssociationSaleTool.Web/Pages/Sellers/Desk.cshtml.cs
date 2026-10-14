using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class DeskModel(DepositDeskService depositDeskService, AppDbContext dbContext) : PageModel
{
    [BindProperty]
    public string? DeskName { get; set; }

    public CurrentDesk Desk { get; private set; } = CurrentDesk.None;

    public IReadOnlyList<char> JustOpenedLetters { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Desk = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
    }

    public async Task<IActionResult> OnPostOpenAsync(CancellationToken cancellationToken)
    {
        var current = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        if (current.State == DeskState.Open)
        {
            Desk = current;
            ModelState.AddModelError(string.Empty, "Un poste est déjà ouvert sur cet ordinateur.");
            return Page();
        }

        OpenDeskResult opened;
        try
        {
            opened = await depositDeskService.OpenDeskAsync(DeskName, cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception is DbUpdateConcurrencyException || IntakeService.IsUniqueConstraintFailure(exception))
        {
            // The failed desk stays tracked as Added in the scoped context: drop it
            // so nothing is retried and the banner query on this request stays clean.
            dbContext.ChangeTracker.Clear();
            Desk = current;
            ModelState.AddModelError(string.Empty, "Un autre poste vient de prendre ces lettres. Réessayez.");
            return Page();
        }

        DepositDeskCookie.Write(Response, opened.Token);
        JustOpenedLetters = opened.Letters;
        Desk = await depositDeskService.GetCurrentDeskAsync(opened.Token, cancellationToken);
        // The banner reads the request cookie, which this response only sets.
        HttpContext.Items[DepositDeskCookie.Name] = opened.Token;
        return Page();
    }

    public async Task<IActionResult> OnPostCloseAsync(CancellationToken cancellationToken)
    {
        var current = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        if (current.DeskId is { } deskId)
        {
            await depositDeskService.CloseDeskAsync(deskId, cancellationToken);
        }

        DepositDeskCookie.Delete(Response);
        HttpContext.Items[DepositDeskCookie.Name] = string.Empty;
        Desk = CurrentDesk.None;
        return Page();
    }
}
