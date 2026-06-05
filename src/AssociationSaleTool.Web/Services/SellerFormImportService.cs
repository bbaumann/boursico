using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Domain.Items;
using AssociationSaleTool.Web.Domain.Sellers;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class SellerFormImportService
{
    private readonly AppDbContext _dbContext;
    private readonly SellerFormImportParser _parser;
    private readonly IntakeService _intakeService;
    private readonly SellerDuplicateService _duplicateService;
    private readonly SalePhaseService _salePhaseService;

    public SellerFormImportService(
        AppDbContext dbContext,
        SellerFormImportParser parser,
        IntakeService? intakeService = null,
        SellerDuplicateService? duplicateService = null,
        SalePhaseService? salePhaseService = null)
    {
        _dbContext = dbContext;
        _parser = parser;
        _duplicateService = duplicateService ?? new SellerDuplicateService(dbContext);
        _salePhaseService = salePhaseService ?? new SalePhaseService(dbContext);
        _intakeService = intakeService ?? new IntakeService(dbContext, _salePhaseService, _duplicateService);
    }

    public async Task<SellerFormImportResult> ImportAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        var parseResult = await _parser.ParseAsync(file, cancellationToken);
        if (!parseResult.Succeeded)
        {
            return new SellerFormImportResult([], parseResult.Errors);
        }

        var rows = new List<SellerFormImportRowResult>();
        foreach (var submission in parseResult.Submissions)
        {
            rows.Add(await ImportSubmissionAsync(submission, cancellationToken));
        }

        return new SellerFormImportResult(rows, []);
    }

    public async Task<SellerFormDuplicateAppendResult> AppendDuplicateItemsAsync(
        long sellerId,
        IReadOnlyList<CreateItemRowRequest> rows,
        string? importNotes,
        CancellationToken cancellationToken = default)
    {
        var seller = await _dbContext.Sellers
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return new SellerFormDuplicateAppendResult(
                SellerFormDuplicateAppendOutcome.NotFound,
                null,
                0,
                0,
                "Le vendeur existant est introuvable.");
        }

        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken)
            || seller.DepositStatus != SellerDepositStatuses.Pending)
        {
            return new SellerFormDuplicateAppendResult(
                SellerFormDuplicateAppendOutcome.NotAllowed,
                seller,
                0,
                0,
                "Les articles ne peuvent pas être ajoutés dans le statut actuel de la vente.");
        }

        var existingKeys = seller.Items
            .Select(item => (Title: item.Title.Trim(), item.SellerPriceCents))
            .ToHashSet();
        var notes = new List<string>();
        var rowsToCreate = new List<CreateItemRowRequest>();
        foreach (var row in rows)
        {
            var title = row.Title.Trim();
            if (existingKeys.Contains((title, row.SellerPriceCents)))
            {
                notes.Add($"Import formulaire : article déjà présent ignoré - {title} - {FormatImportPrice(row.SellerPriceCents)}.");
                continue;
            }

            existingKeys.Add((title, row.SellerPriceCents));
            rowsToCreate.Add(new CreateItemRowRequest(title, row.SellerPriceCents));
        }

        if (rowsToCreate.Count > 0)
        {
            var nextItemNumber = seller.Items
                .Select(item => (int?)item.ItemNumber)
                .Max() ?? 0;
            foreach (var row in rowsToCreate)
            {
                nextItemNumber++;
                _dbContext.Items.Add(new Data.Entities.ItemEntity
                {
                    Seller = seller,
                    SellerId = seller.Id,
                    Reference = ItemReference.Format(seller.SellerCode, nextItemNumber),
                    ItemNumber = nextItemNumber,
                    Title = row.Title.Trim(),
                    SellerPriceCents = row.SellerPriceCents,
                    Status = "registered"
                });
            }
        }

        seller.ImportNotes = CombineImportNotes(seller.ImportNotes, importNotes, notes);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SellerFormDuplicateAppendResult(
            SellerFormDuplicateAppendOutcome.Appended,
            seller,
            rowsToCreate.Count,
            notes.Count,
            null);
    }

    private async Task<SellerFormImportRowResult> ImportSubmissionAsync(
        SellerFormImportSubmission submission,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(submission.FirstName) || string.IsNullOrWhiteSpace(submission.LastName))
        {
            return new SellerFormImportRowResult(
                submission,
                SellerFormImportRowOutcome.SellerError,
                null,
                null,
                [],
                "Le prénom et le nom sont obligatoires pour importer une soumission.");
        }

        var duplicateMatches = await _duplicateService.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest(submission.FirstName, submission.LastName, submission.Phone),
            cancellationToken);
        if (duplicateMatches.Count > 0)
        {
            return new SellerFormImportRowResult(
                submission,
                SellerFormImportRowOutcome.Duplicate,
                null,
                null,
                duplicateMatches,
                "Un vendeur existant correspond à cette soumission.");
        }

        try
        {
            if (submission.ItemRows.Count > 0)
            {
                var result = await _intakeService.CreateSellerWithItemsAsync(
                    new CreateSellerWithItemsRequest(
                        submission.FirstName,
                        submission.LastName,
                        submission.Email,
                        submission.Phone,
                        submission.PostalAddress,
                        submission.Notes,
                        IntakeMode.EmailPreregistration,
                        submission.ItemRows,
                        FeePaidCents: 0,
                        PostalCode: submission.PostalCode,
                        City: submission.City,
                        ImportNotes: submission.ImportNotes),
                    cancellationToken);

                if (result.Outcome == CreateSellerWithItemsOutcome.ExistingSellerFound)
                {
                    return new SellerFormImportRowResult(
                        submission,
                        SellerFormImportRowOutcome.Duplicate,
                        null,
                        null,
                        result.DuplicateMatches,
                        "Un vendeur existant correspond à cette soumission.");
                }

                return BuildImportedResult(submission, result.Seller!);
            }

            var createResult = await _intakeService.CreateSellerAsync(
                new CreateSellerRequest(
                    submission.FirstName,
                    submission.LastName,
                    submission.Email,
                    submission.Phone,
                    submission.PostalAddress,
                    submission.Notes,
                    FeePaidCents: 0,
                    PostalCode: submission.PostalCode,
                    City: submission.City,
                    ImportNotes: submission.ImportNotes),
                cancellationToken);

            if (createResult.Outcome == CreateSellerOutcome.DuplicateFound)
            {
                return new SellerFormImportRowResult(
                    submission,
                    SellerFormImportRowOutcome.Duplicate,
                    null,
                    null,
                    createResult.DuplicateMatches,
                    "Un vendeur existant correspond à cette soumission.");
            }

            return BuildImportedResult(submission, createResult.Seller!);
        }
        catch (InvalidOperationException exception)
        {
            return new SellerFormImportRowResult(
                submission,
                SellerFormImportRowOutcome.ImportError,
                null,
                null,
                [],
                exception.Message);
        }
    }

    private static SellerFormImportRowResult BuildImportedResult(
        SellerFormImportSubmission submission,
        Data.Entities.SellerEntity seller)
    {
        return new SellerFormImportRowResult(
            submission,
            SellerFormImportRowOutcome.Imported,
            seller.Id,
            seller.SellerCode,
            [],
            null);
    }

    public static string? CombineImportNotes(
        string? existingNotes,
        string? incomingNotes,
        IEnumerable<string>? additionalNotes = null)
    {
        var parts = new List<string>();
        Add(existingNotes);
        Add(incomingNotes);
        if (additionalNotes is not null)
        {
            foreach (var note in additionalNotes)
            {
                Add(note);
            }
        }

        return parts.Count == 0 ? null : string.Join(Environment.NewLine, parts);

        void Add(string? note)
        {
            if (!string.IsNullOrWhiteSpace(note))
            {
                parts.Add(note.Trim());
            }
        }
    }

    private static string FormatImportPrice(int? priceCents)
    {
        return priceCents is null ? "prix à compléter" : $"{priceCents / 100} €";
    }
}
