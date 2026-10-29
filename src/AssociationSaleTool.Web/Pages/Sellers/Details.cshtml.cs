using System.Globalization;
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Sellers;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class DetailsModel(
    IntakeService intakeService,
    DepositDeskService depositDeskService,
    AppDbContext dbContext,
    IOptions<AdminOptions>? adminOptions = null) : PageModel
{
    private const int DefaultRowCount = 5;
    private const string NoOpenDeskMessage = "Prenez des lettres pour continuer le dépôt.";
    private const string DepositNotStartedMessage = "Étiquetez les jouets avant de finaliser le dépôt.";
    private const string DuplicateRowsAlertMessage = "Ce vendeur existe déjà. Les articles saisis ont été ajoutés au formulaire ci-dessous. Vérifiez puis enregistrez le lot.";
    private readonly AdminOptions _adminOptions = adminOptions?.Value ?? new AdminOptions();

    [BindProperty]
    public ItemBatchInput Input { get; set; } = CreateDefaultInput();

    [BindProperty]
    public CancellationInputModel CancellationInput { get; set; } = new();

    [BindProperty]
    public DepositFeeInputModel DepositFeeInput { get; set; } = new();

    public SellerEntity? Seller { get; private set; }

    public IReadOnlyList<ItemEntity> Items { get; private set; } = [];

    public bool CanEditItemMasterData { get; private set; }

    public bool CanEditSellerMasterData { get; private set; }

    public bool CanValidateDeposit { get; private set; }

    public bool CanCancelDepositValidation { get; private set; }

    public bool ShouldOpenDepositFinalizationDialog { get; private set; }

    public string DepositFinalizationInitialStep { get; private set; } = DepositFinalizationSteps.Fees;

    public string? ReadOnlyMessage { get; private set; }

    public bool IsDepositValidated => Seller?.DepositStatus == SellerDepositStatuses.Validated;

    public string DepositStatusLabel => Seller is null
        ? string.Empty
        : SellerDepositStatuses.ToFrenchLabel(Seller.DepositStatus, Seller.DepositStartedAt);

    public SellerFeeStatus FeeStatus { get; private set; } = SellerFeeStatus.Empty;

    public string? PendingRowsAlert { get; private set; }

    [TempData]
    public string? DepositSuccessMessage { get; set; }

    [TempData]
    public string? AllocationMessage { get; set; }

    [TempData]
    public string? DeskMessage { get; set; }

    // Comma-joined, since TempData only round-trips simple types.
    [TempData]
    public string? LabelledReferences { get; set; }

    public bool IsJustLabelled(string? reference)
    {
        return reference is not null
            && LabelledReferences is not null
            && LabelledReferences.Split(',').Contains(reference, StringComparer.Ordinal);
    }

    public CurrentDesk Desk { get; private set; } = CurrentDesk.None;

    public bool IsDeskOpen => Desk.State == DeskState.Open;

    public IEnumerable<DeskLetterInfo> LowLabelLetters =>
        Desk.Letters.Where(x => x.LabelsLeft <= DepositDeskService.LowLabelsThreshold);

    public bool IsDepositStarted => Seller?.DepositStartedAt is not null;

    public IReadOnlyList<string> DepositValidationMessages { get; private set; } = [];

    public bool HasSellerDepositValidationMessages { get; private set; }

    public bool HasItemDepositValidationMessages { get; private set; }

    // Before labelling, "not started" is already told by the status and the
    // greyed-out button, so it is not listed as a point to correct.
    public IReadOnlyList<string> VisibleDepositBlockers => IsDepositStarted
        ? DepositValidationMessages
        : DepositValidationMessages.Where(message => message != DepositNotStartedMessage).ToList();

    public bool CanBeginDepositFinalization => IsDeskOpen && DepositValidationMessages.Count == 0;

    // The deposit runs 1. complete the seller file, 2. label the toys,
    // 3. finalize: while the file is incomplete, completing it is the only
    // green action and the later steps step back to secondary buttons.
    public string NextStepButtonClass(bool isSellerFileStep)
    {
        var isNextStep = HasSellerDepositValidationMessages == isSellerFileStep;
        return isNextStep ? "button-link button-link-solid" : "button-link";
    }

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        await PopulatePermissionStateAsync(loaded, cancellationToken);
        Input = CreateDefaultInput();
        ApplyPendingRows(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        await PopulatePermissionStateAsync(loaded, cancellationToken);

        if (!CanEditItemMasterData)
        {
            EnsureDefaultRows();
            ModelState.AddModelError(string.Empty, "Les articles ne peuvent pas être ajoutés dans le statut actuel de la vente.");
            return Page();
        }

        var rowsToCreate = ValidateRows();
        EnsureDefaultRows();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var desk = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        var result = await intakeService.CreateItemBatchAsync(
            new CreateItemBatchRequest(id, IntakeMode.EmailPreregistration, rowsToCreate, OpenDeskId(desk)),
            cancellationToken);
        if (result.Status == DepositAllocationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status != DepositAllocationStatus.Done)
        {
            // Nothing was saved: the rows the operator typed stay in Input so
            // they can re-submit once the desk problem is solved.
            ModelState.AddModelError(
                string.Empty,
                ToAllocationError(result.Status, desk.State, "Les articles ne peuvent pas être ajoutés dans le statut actuel de la vente."));
            return Page();
        }

        if (result.AssignedReferences.Count > 0)
        {
            AllocationMessage = ToAllocationMessage(result);
            LabelledReferences = string.Join(",", result.AssignedReferences);
        }

        return RedirectToPage("/Sellers/Details", null, new { id }, "articles");
    }

    public async Task<IActionResult> OnPostStartDepositAsync(long id, CancellationToken cancellationToken)
    {
        var desk = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        var result = await intakeService.StartDepositAsync(id, OpenDeskId(desk), cancellationToken);
        if (result.Status == DepositAllocationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == DepositAllocationStatus.Done)
        {
            AllocationMessage = ToAllocationMessage(result);
            LabelledReferences = string.Join(",", result.AssignedReferences);
            return RedirectToPage("/Sellers/Details", null, new { id }, "articles");
        }

        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        Input = CreateDefaultInput();
        await PopulatePermissionStateAsync(loaded, cancellationToken);
        var notAllowedMessage = loaded.DepositStartedAt is not null
            ? "Les jouets de ce vendeur sont déjà étiquetés."
            : "Les jouets ne peuvent pas être étiquetés dans le statut actuel de la vente.";
        ModelState.AddModelError(string.Empty, ToAllocationError(result.Status, desk.State, notAllowedMessage));
        return Page();
    }

    public async Task<IActionResult> OnPostOpenDeskAsync(long id, CancellationToken cancellationToken)
    {
        var current = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        if (current.State == DeskState.Open)
        {
            return RedirectToPage("/Sellers/Details", new { id });
        }

        OpenDeskResult opened;
        try
        {
            opened = await depositDeskService.OpenDeskAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception is DbUpdateConcurrencyException || IntakeService.IsUniqueConstraintFailure(exception))
        {
            // The failed desk stays tracked as Added in the scoped context: drop it
            // so nothing is retried and the page queries on this request stay clean.
            dbContext.ChangeTracker.Clear();
            return await PageWithErrorAsync(id, "Un autre ordinateur vient de prendre ces lettres. Réessayez.", cancellationToken);
        }

        DepositDeskCookie.Write(Response, opened.Token);
        DeskMessage = opened.Letters.Count == 0
            ? "Aucune lettre libre pour l'instant : cet ordinateur en recevra une dès qu'un autre ordinateur en libère une."
            : $"Allez chercher les planches {string.Join(" et ", opened.Letters)}.";
        return RedirectToPage("/Sellers/Details", new { id });
    }

    public async Task<IActionResult> OnPostReleaseDeskAsync(long id, CancellationToken cancellationToken)
    {
        var current = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        if (current.State == DeskState.Open && current.DeskId is { } deskId)
        {
            await depositDeskService.CloseDeskAsync(deskId, cancellationToken);
            DeskMessage = current.Letters.Count == 0
                ? "Lettres libérées."
                : $"Lettres {string.Join(" et ", current.Letters.Select(x => x.Letter))} libérées. Rapportez leurs planches sur la table centrale.";
        }

        DepositDeskCookie.Delete(Response);
        return RedirectToPage("/Sellers/Details", new { id });
    }

    private async Task<IActionResult> PageWithErrorAsync(long id, string message, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        Input = CreateDefaultInput();
        await PopulatePermissionStateAsync(loaded, cancellationToken);
        ModelState.AddModelError(string.Empty, message);
        return Page();
    }

    public static string ToAllocationMessage(DepositAllocationResult result)
    {
        var references = result.AssignedReferences;
        if (references.Count == 0)
        {
            return "Dépôt commencé : aucun jouet à étiqueter pour l'instant. Chaque jouet ajouté recevra son étiquette à l'enregistrement.";
        }

        var parts = new List<string>();
        if (references.Count == 1)
        {
            parts.Add($"1 jouet étiqueté : {references[0]}.");
        }
        else
        {
            // A run split across letters cannot be written as a range.
            var contiguous = references
                .Skip(1)
                .Select((reference, index) => (Previous: references[index], Current: reference))
                .All(pair => pair.Current[0] == pair.Previous[0]
                    && int.Parse(pair.Current[1..], CultureInfo.InvariantCulture) == int.Parse(pair.Previous[1..], CultureInfo.InvariantCulture) + 1);
            parts.Add(contiguous
                ? $"{references.Count} jouets étiquetés : {references[0]} à {references[^1]}."
                : $"{references.Count} jouets étiquetés : {string.Join(", ", references)}.");
        }

        foreach (var letter in result.TakenLetters)
        {
            parts.Add($"Allez chercher la planche {letter}.");
        }

        var letters = references.Select(reference => reference[0]).Distinct().ToList();
        var sheets = letters.Count == 1
            ? $"de la planche {letters[0]}"
            : $"des planches {string.Join(" et ", letters)}";
        parts.Add(references.Count == 1
            ? $"Décollez l'étiquette suivante {sheets} et collez-la sur le jouet surligné ci-dessous."
            : $"Décollez les étiquettes {sheets} dans l'ordre et collez-les sur les jouets surlignés ci-dessous.");

        return string.Join(" ", parts);
    }

    private static long? OpenDeskId(CurrentDesk desk)
    {
        return desk.State == DeskState.Open ? desk.DeskId : null;
    }

    // Maps every refusal; notAllowedMessage covers NotAllowed, whose wording
    // depends on what the operator was doing.
    public static string ToAllocationError(DepositAllocationStatus status, DeskState deskState, string notAllowedMessage)
    {
        // DeskClosed comes from the save itself, so it wins over what the
        // cookie showed at page load (the desk may have closed in between).
        return status switch
        {
            DepositAllocationStatus.DeskClosed =>
                "Les lettres de cet ordinateur ont été libérées. Reprenez des lettres pour continuer.",
            DepositAllocationStatus.NoDesk when deskState == DeskState.Closed =>
                "Les lettres de cet ordinateur ont été libérées. Reprenez des lettres pour continuer.",
            DepositAllocationStatus.NoDesk =>
                "Prenez des lettres pour attribuer des références.",
            DepositAllocationStatus.NoLabelsLeft =>
                "Plus aucune étiquette disponible dans les lettres de cet ordinateur. Demandez à l'administration d'augmenter le nombre d'étiquettes par lettre ou de libérer une lettre.",
            DepositAllocationStatus.Conflict =>
                "Un autre ordinateur vient de modifier ces lettres. Réessayez.",
            DepositAllocationStatus.NotAllowed => notAllowedMessage,
            DepositAllocationStatus.NotFound => "Vendeur introuvable.",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    public async Task<IActionResult> OnPostDeleteItemAsync(long id, long itemId, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        Input = CreateDefaultInput();
        await PopulatePermissionStateAsync(loaded, cancellationToken);

        if (!CanEditItemMasterData)
        {
            ModelState.AddModelError(string.Empty, "Les articles ne peuvent pas être supprimés dans le statut actuel de la vente.");
            return Page();
        }

        var result = await intakeService.DeleteSellerItemAsync(id, itemId, cancellationToken);
        if (result is DeleteItemResult.NotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Seuls les articles enregistrés ou en stock peuvent être supprimés.");
            return Page();
        }

        return RedirectToPage("/Sellers/Details", new { id });
    }

    public async Task<IActionResult> OnPostValidateDepositAsync(long id, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        Input = CreateDefaultInput();
        await PopulatePermissionStateAsync(loaded, cancellationToken);

        var result = await intakeService.ValidateSellerDepositAsync(id, cancellationToken);
        if (result == SellerDepositStatusChangeResult.Changed)
        {
            DepositSuccessMessage = loaded.Items.Count == 1
                ? "Dépôt validé : 1 article est maintenant en stock."
                : $"Dépôt validé : {loaded.Items.Count} articles sont maintenant en stock.";
            return RedirectToPage("/Sellers/Details", new { id });
        }

        if (result == SellerDepositStatusChangeResult.NotFound)
        {
            return NotFound();
        }

        var validationState = BuildDepositValidationState(loaded);
        ApplyDepositValidationState(validationState);
        if (DepositValidationMessages.Count == 0)
        {
            // The seller and item data are otherwise valid and the deposit is
            // started here - this branch is reached only when IntakeService's
            // own guard refused the transition, which today means a registered
            // item still has no reference. It is the last line of defense, as
            // a started deposit labels every row it saves. Say that
            // specifically rather than pointing the operator at data that
            // isn't the problem.
            var referencesNotYetAllocated = loaded.Items.Any(item => item.Status == "registered" && item.Reference is null);
            DepositValidationMessages = referencesNotYetAllocated
                ? [DepositNotStartedMessage]
                : [
                    "Le dépôt ne peut pas être validé : vérifiez les informations du vendeur et les articles avant de signer le contrat."
                ];
            HasSellerDepositValidationMessages = !referencesNotYetAllocated;
            HasItemDepositValidationMessages = !referencesNotYetAllocated;
        }

        AddDepositValidationMessagesToModelState();
        return Page();
    }

    public async Task<IActionResult> OnPostBeginDepositFinalizationAsync(long id, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        Input = CreateDefaultInput();
        await PopulatePermissionStateAsync(loaded, cancellationToken);

        if (!CanValidateDeposit)
        {
            ModelState.AddModelError(string.Empty, "Le dépôt ne peut pas être finalisé dans le statut actuel de la vente.");
            return Page();
        }

        if (!IsDeskOpen)
        {
            ModelState.AddModelError(string.Empty, NoOpenDeskMessage);
            return Page();
        }

        var validationState = BuildDepositValidationState(loaded);
        ApplyDepositValidationState(validationState);
        if (DepositValidationMessages.Count > 0)
        {
            AddDepositValidationMessagesToModelState();
            return Page();
        }

        ShouldOpenDepositFinalizationDialog = true;
        DepositFinalizationInitialStep = DepositFinalizationSteps.Fees;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateDepositFeeAsync(long id, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        var postedFeeInput = DepositFeeInput;
        PopulateSellerState(loaded);
        DepositFeeInput = postedFeeInput;
        Input = CreateDefaultInput();
        await PopulatePermissionStateAsync(loaded, cancellationToken);

        if (!CanValidateDeposit)
        {
            ModelState.AddModelError(string.Empty, "Le dépôt ne peut pas être finalisé dans le statut actuel de la vente.");
            return Page();
        }

        var validationState = BuildDepositValidationState(loaded);
        ApplyDepositValidationState(validationState);
        if (DepositValidationMessages.Count > 0)
        {
            AddDepositValidationMessagesToModelState();
            return Page();
        }

        ShouldOpenDepositFinalizationDialog = true;
        DepositFinalizationInitialStep = DepositFinalizationSteps.Fees;

        var feePaidCents = ParseDepositFeePaid();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await intakeService.UpdateSellerFeePaidAsync(id, feePaidCents, cancellationToken);
        if (result == UpdateSellerResult.NotFound)
        {
            return NotFound();
        }

        if (result == UpdateSellerResult.NotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Les frais vendeur ne peuvent pas être modifiés dans le statut actuel de la vente.");
            return Page();
        }

        var updated = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (updated is null)
        {
            return NotFound();
        }

        PopulateSellerState(updated);
        await PopulatePermissionStateAsync(updated, cancellationToken);
        ShouldOpenDepositFinalizationDialog = true;
        DepositFinalizationInitialStep = DepositFinalizationSteps.Print;
        return Page();
    }

    public async Task<IActionResult> OnPostCancelDepositValidationAsync(long id, CancellationToken cancellationToken)
    {
        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        Input = CreateDefaultInput();
        await PopulatePermissionStateAsync(loaded, cancellationToken);

        if (!CanCancelDepositValidation)
        {
            ModelState.AddModelError(string.Empty, "Action réservée à l'administration pendant la prévente.");
            return Page();
        }

        if (CancellationInput.AdminPassword != _adminOptions.SharedPassword)
        {
            ModelState.AddModelError("CancellationInput.AdminPassword", "Mot de passe administrateur incorrect.");
            return Page();
        }

        var result = await intakeService.CancelSellerDepositValidationAsync(id, cancellationToken);
        if (result == SellerDepositStatusChangeResult.Changed)
        {
            return RedirectToPage("/Sellers/Details", new { id });
        }

        if (result == SellerDepositStatusChangeResult.NotFound)
        {
            return NotFound();
        }

        ModelState.AddModelError(
            string.Empty,
            "La validation du dépôt ne peut pas être annulée si un article a déjà changé d'état.");
        return Page();
    }

    public static string FormatPrice(int? priceCents)
    {
        return priceCents is null
            ? "À compléter"
            : $"{(priceCents.Value / 100m).ToString("0.00", CultureInfo.InvariantCulture)} €";
    }

    public static string FormatDate(DateOnly? date)
    {
        return date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "À compléter";
    }

    private void PopulateSellerState(SellerEntity seller)
    {
        Seller = seller;
        // By reference once labelled, so the operator matches toys to the
        // peeled run in order; unlabelled rows follow in creation order.
        Items = seller.Items
            .OrderBy(item => item.Reference is null)
            .ThenBy(item => item.Reference, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToList();
        var expectedFeeCents = SellerFeeCalculator.CalculateExpectedFeeCents(seller.Items.Count);
        var remainingFeeCents = SellerFeeCalculator.CalculateRemainingFeeCents(expectedFeeCents, seller.FeePaidCents);
        FeeStatus = new SellerFeeStatus(
            expectedFeeCents,
            seller.FeePaidCents,
            remainingFeeCents,
            remainingFeeCents == 0);
        DepositFeeInput = new DepositFeeInputModel
        {
            FeePaid = seller.FeePaidCents == 0 ? null : EuroPriceInput.FormatFromCents(seller.FeePaidCents)
        };
    }

    private async Task PopulatePermissionStateAsync(SellerEntity seller, CancellationToken cancellationToken)
    {
        Desk = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        var canEditCurrentPhase = await intakeService.CanEditItemMasterDataAsync(cancellationToken);
        var isPendingDeposit = seller.DepositStatus == SellerDepositStatuses.Pending;
        CanEditItemMasterData = canEditCurrentPhase && isPendingDeposit;
        CanEditSellerMasterData = canEditCurrentPhase && isPendingDeposit;
        CanValidateDeposit = canEditCurrentPhase && isPendingDeposit;
        CanCancelDepositValidation = canEditCurrentPhase
            && seller.DepositStatus == SellerDepositStatuses.Validated;
        // Blockers are shown as soon as the page opens, not only after a
        // refused `Finaliser le dépôt`, so the operator completes the seller
        // file while the seller is still at the desk.
        if (CanValidateDeposit)
        {
            ApplyDepositValidationState(BuildDepositValidationState(seller));
        }

        ReadOnlyMessage = !isPendingDeposit
            ? "Le contrat de dépôt signé rend le vendeur et ses articles en lecture seule."
            : !canEditCurrentPhase
                ? "Les informations du vendeur et les articles sont en lecture seule dans le statut actuel de la vente."
                : null;
    }

    private List<CreateItemRowRequest> ValidateRows()
    {
        var rowsToCreate = new List<CreateItemRowRequest>();

        for (var index = 0; index < Input.Rows.Count; index++)
        {
            var row = Input.Rows[index];
            var titleMissing = string.IsNullOrWhiteSpace(row.Title);
            var priceMissing = string.IsNullOrWhiteSpace(row.SellerPrice);

            if (titleMissing && priceMissing)
            {
                continue;
            }

            if (titleMissing)
            {
                ModelState.AddModelError($"Input.Rows[{index}].Title", "La description est obligatoire lorsqu'un prix est saisi.");
                continue;
            }

            if (priceMissing)
            {
                ModelState.AddModelError($"Input.Rows[{index}].SellerPrice", "Le prix vendeur est obligatoire lorsqu'une description est saisie.");
                continue;
            }

            if (!EuroPriceInput.TryParseToCents(row.SellerPrice, out var sellerPriceCents))
            {
                ModelState.AddModelError($"Input.Rows[{index}].SellerPrice", "Le prix vendeur doit être un montant entier en euros.");
                continue;
            }

            if (sellerPriceCents < 0)
            {
                ModelState.AddModelError($"Input.Rows[{index}].SellerPrice", "Le prix vendeur doit être supérieur ou égal à zéro.");
                continue;
            }

            rowsToCreate.Add(new CreateItemRowRequest(row.Title!, sellerPriceCents));
        }

        if (rowsToCreate.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Saisissez au moins une ligne d'article complète.");
        }

        return rowsToCreate;
    }

    private int ParseDepositFeePaid()
    {
        if (string.IsNullOrWhiteSpace(DepositFeeInput.FeePaid))
        {
            return 0;
        }

        if (!EuroPriceInput.TryParseToCents(DepositFeeInput.FeePaid, out var feePaidCents))
        {
            ModelState.AddModelError("DepositFeeInput.FeePaid", "Les frais payés doivent être un montant entier en euros.");
            return 0;
        }

        if (feePaidCents < 0)
        {
            ModelState.AddModelError("DepositFeeInput.FeePaid", "Les frais payés doivent être supérieurs ou égaux à zéro.");
            return 0;
        }

        return feePaidCents;
    }

    private void EnsureDefaultRows()
    {
        while (Input.Rows.Count < DefaultRowCount)
        {
            Input.Rows.Add(new ItemRowInput());
        }
    }

    private void ApplyPendingRows(long sellerId)
    {
        PendingRowsAlert = null;
        var httpContext = PageContext?.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var session = httpContext.Features.Get<ISessionFeature>()?.Session;
        if (session is null)
        {
            return;
        }

        var pendingRows = PendingSellerItemRowsSession.Consume(session, sellerId);
        if (pendingRows is null)
        {
            return;
        }

        Input.IntakeMode = IntakeMode.EmailPreregistration;
        Input.Rows = pendingRows.Rows
            .Select(row => new ItemRowInput
            {
                Title = row.Title,
                SellerPrice = row.SellerPriceCents is null
                    ? null
                    : EuroPriceInput.FormatFromCents(row.SellerPriceCents.Value)
            })
            .ToList();
        EnsureDefaultRows();
        PendingRowsAlert = DuplicateRowsAlertMessage;
    }

    private void ApplyDepositValidationState(DepositValidationState validationState)
    {
        DepositValidationMessages = validationState.Messages;
        HasSellerDepositValidationMessages = validationState.HasSellerMessages;
        HasItemDepositValidationMessages = validationState.HasItemMessages;
    }

    private void AddDepositValidationMessagesToModelState()
    {
        foreach (var message in DepositValidationMessages)
        {
            ModelState.AddModelError(string.Empty, message);
        }
    }

    private static ItemBatchInput CreateDefaultInput()
    {
        return new ItemBatchInput
        {
            IntakeMode = IntakeMode.EmailPreregistration,
            Rows = Enumerable.Range(0, DefaultRowCount)
                .Select(_ => new ItemRowInput())
                .ToList()
        };
    }

    private static DepositValidationState BuildDepositValidationState(SellerEntity seller)
    {
        var messages = new List<string>();
        var hasSellerMessages = false;
        var hasItemMessages = false;

        AddRequiredMessage(messages, seller.FirstName, "Le prénom est obligatoire.", ref hasSellerMessages);
        AddRequiredMessage(messages, seller.LastName, "Le nom est obligatoire.", ref hasSellerMessages);
        AddRequiredMessage(messages, seller.Phone, "Le téléphone est obligatoire.", ref hasSellerMessages);
        AddRequiredMessage(messages, seller.PostalAddress, "L'adresse postale est obligatoire.", ref hasSellerMessages);
        AddRequiredMessage(messages, seller.PostalCode, "Le code postal est obligatoire.", ref hasSellerMessages);
        AddRequiredMessage(messages, seller.City, "La ville est obligatoire.", ref hasSellerMessages);
        AddRequiredMessage(messages, seller.IdentityDocumentType, "La nature de la pièce d'identité est obligatoire.", ref hasSellerMessages);
        AddRequiredMessage(messages, seller.IdentityDocumentNumber, "Le numéro de la pièce d'identité est obligatoire.", ref hasSellerMessages);
        if (seller.IdentityDocumentIssuedOn is null)
        {
            messages.Add("La date de délivrance est obligatoire.");
            hasSellerMessages = true;
        }

        if (seller.Items.Count == 0)
        {
            messages.Add("Ajoutez au moins un article avant de valider le dépôt.");
            hasItemMessages = true;
        }

        if (seller.Items.Any(item => item.Status is not ("registered" or "in_stock" or "sold" or "missing" or "defective")))
        {
            messages.Add("Tous les articles doivent avoir un statut valide avant de valider le dépôt.");
            hasItemMessages = true;
        }

        if (seller.Items.Any(item => string.IsNullOrWhiteSpace(item.Title)))
        {
            messages.Add("Tous les articles doivent avoir une description avant de valider le dépôt.");
            hasItemMessages = true;
        }

        if (seller.Items.Any(item => item.SellerPriceCents is null))
        {
            messages.Add("Tous les articles doivent avoir un prix vendeur avant de valider le dépôt.");
            hasItemMessages = true;
        }

        if (seller.Items.Any(item => item.SellerPriceCents is < 0))
        {
            messages.Add("Tous les prix vendeur doivent être supérieurs ou égaux à zéro.");
            hasItemMessages = true;
        }

        // Not an item blocker: no row is incomplete, so the page must not show
        // the "Articles" summary or the "Corriger les articles" link for it.
        if (seller.DepositStartedAt is null)
        {
            messages.Add(DepositNotStartedMessage);
        }

        return new DepositValidationState(messages, hasSellerMessages, hasItemMessages);
    }

    private static void AddRequiredMessage(List<string> messages, string? value, string message, ref bool hasMessages)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            messages.Add(message);
            hasMessages = true;
        }
    }

    private sealed record DepositValidationState(
        IReadOnlyList<string> Messages,
        bool HasSellerMessages,
        bool HasItemMessages);

    private static class DepositFinalizationSteps
    {
        public const string Fees = "fees";

        public const string Print = "print";
    }

    public sealed class ItemBatchInput
    {
        public IntakeMode IntakeMode { get; set; }

        public List<ItemRowInput> Rows { get; set; } = [];
    }

    public sealed class ItemRowInput
    {
        public string? Title { get; set; }

        public string? SellerPrice { get; set; }
    }

    public sealed class CancellationInputModel
    {
        public string? AdminPassword { get; set; }
    }

    public sealed class DepositFeeInputModel
    {
        public string? FeePaid { get; set; } = "0";
    }

    public sealed record SellerFeeStatus(
        int ExpectedFeeCents,
        int PaidFeeCents,
        int RemainingFeeCents,
        bool IsFullyPaid)
    {
        public static SellerFeeStatus Empty { get; } = new(0, 0, 0, true);
    }
}
