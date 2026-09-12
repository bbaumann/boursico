using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Items;
using AssociationSaleTool.Web.Domain.Sellers;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class IntakeService
{
    private readonly AppDbContext _dbContext;
    private readonly SalePhaseService _salePhaseService;
    private readonly SellerDuplicateService _sellerDuplicateService;
    private readonly IserePostalCityLookup _postalCityLookup;

    public IntakeService(
        AppDbContext dbContext,
        SalePhaseService? salePhaseService = null,
        SellerDuplicateService? sellerDuplicateService = null,
        IserePostalCityLookup? postalCityLookup = null)
    {
        _dbContext = dbContext;
        _salePhaseService = salePhaseService ?? new SalePhaseService(dbContext);
        _sellerDuplicateService = sellerDuplicateService ?? new SellerDuplicateService(dbContext);
        _postalCityLookup = postalCityLookup ?? IserePostalCityLookup.Empty;
    }

    public async Task<List<SellerEntity>> ListSellersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sellers
            .AsNoTracking()
            .Include(x => x.Items)
            .OrderBy(x => x.SellerCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<SellerEntity?> GetSellerAsync(long sellerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sellers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
    }

    public async Task<SellerEntity?> GetSellerWithItemsAsync(long sellerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sellers
            .AsNoTracking()
            .Include(x => x.Items.OrderBy(item => item.ItemNumber))
            .SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
    }

    public async Task<ItemEntity?> GetItemAsync(long itemId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Items
            .AsNoTracking()
            .Include(x => x.Seller)
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
    }

    public async Task<CreateSellerResult> CreateSellerAsync(CreateSellerRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken))
        {
            throw new InvalidOperationException("Seller master data is locked outside presale.");
        }

        var normalizedPhone = NormalizeOptional(request.Phone) ?? string.Empty;
        var normalizedPostalAddress = NormalizeOptional(request.PostalAddress) ?? string.Empty;
        var normalizedPostalCity = NormalizeSellerPostalCity(request.PostalCode, request.City);
        var normalizedIdentityDocumentType = NormalizeOptional(request.IdentityDocumentType);
        var normalizedIdentityDocumentNumber = NormalizeOptional(request.IdentityDocumentNumber);
        var normalizedIdentityDocumentIssuer = NormalizeOptional(request.IdentityDocumentIssuer);
        EnsureSellerFeePaidIsValid(request.FeePaidCents);

        var duplicateMatches = await _sellerDuplicateService.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest(request.FirstName, request.LastName, normalizedPhone),
            cancellationToken);
        if (duplicateMatches.Count > 0)
        {
            return CreateSellerResult.DuplicateFound(duplicateMatches);
        }

        var seller = new SellerEntity
        {
            SellerCode = await AllocateSellerCodeAsync(cancellationToken),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = NormalizeOptional(request.Email),
            Phone = normalizedPhone,
            PostalAddress = normalizedPostalAddress,
            PostalCode = normalizedPostalCity.PostalCode,
            City = normalizedPostalCity.City,
            IdentityDocumentType = normalizedIdentityDocumentType,
            IdentityDocumentNumber = normalizedIdentityDocumentNumber,
            IdentityDocumentIssuedOn = request.IdentityDocumentIssuedOn,
            IdentityDocumentIssuer = normalizedIdentityDocumentIssuer,
            FeePaidCents = request.FeePaidCents,
            DepositStatus = SellerDepositStatuses.Pending,
            Notes = NormalizeOptional(request.Notes),
            ImportNotes = NormalizeOptional(request.ImportNotes)
        };

        _dbContext.Sellers.Add(seller);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return CreateSellerResult.Created(seller);
    }

    public async Task<IReadOnlyList<SellerDuplicateMatch>> FindDuplicateSellersAsync(
        string firstName,
        string lastName,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        return await _sellerDuplicateService.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest(firstName, lastName, phone),
            cancellationToken);
    }

    public async Task<CreateSellerWithItemsResult> CreateSellerWithItemsAsync(
        CreateSellerWithItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken))
        {
            throw new InvalidOperationException("Seller master data is locked outside presale.");
        }

        var normalizedPhone = NormalizeOptional(request.Phone) ?? string.Empty;
        var normalizedPostalAddress = NormalizeOptional(request.PostalAddress) ?? string.Empty;
        var normalizedPostalCity = NormalizeSellerPostalCity(request.PostalCode, request.City);
        var normalizedIdentityDocumentType = NormalizeOptional(request.IdentityDocumentType);
        var normalizedIdentityDocumentNumber = NormalizeOptional(request.IdentityDocumentNumber);
        var normalizedIdentityDocumentIssuer = NormalizeOptional(request.IdentityDocumentIssuer);
        EnsureSellerFeePaidIsValid(request.FeePaidCents);
        EnsureRowsAreValid(request.Rows);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var duplicateMatches = await _sellerDuplicateService.FindDuplicateSellersAsync(
                    new SellerDuplicateCheckRequest(request.FirstName, request.LastName, normalizedPhone),
                    cancellationToken);
                if (duplicateMatches.Count > 0)
                {
                    return CreateSellerWithItemsResult.ExistingSellerFound(duplicateMatches);
                }

                var seller = new SellerEntity
                {
                    SellerCode = await AllocateSellerCodeAsync(cancellationToken),
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = NormalizeOptional(request.Email),
                    Phone = normalizedPhone,
                    PostalAddress = normalizedPostalAddress,
                    PostalCode = normalizedPostalCity.PostalCode,
                    City = normalizedPostalCity.City,
                    IdentityDocumentType = normalizedIdentityDocumentType,
                    IdentityDocumentNumber = normalizedIdentityDocumentNumber,
                    IdentityDocumentIssuedOn = request.IdentityDocumentIssuedOn,
                    IdentityDocumentIssuer = normalizedIdentityDocumentIssuer,
                    FeePaidCents = request.FeePaidCents,
                    DepositStatus = SellerDepositStatuses.Pending,
                    Notes = NormalizeOptional(request.Notes),
                    ImportNotes = NormalizeOptional(request.ImportNotes)
                };

                _dbContext.Sellers.Add(seller);
                AddItemRows(seller, request.Rows, 0);

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return CreateSellerWithItemsResult.Created(seller);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintFailure(exception) && attempt < 2)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Le code vendeur n'a pas pu être attribué. Réessayez l'enregistrement.");
    }

    public async Task<List<ItemEntity>> CreateItemBatchAsync(CreateItemBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken))
        {
            throw new InvalidOperationException("Item master data is locked outside presale.");
        }

        EnsureRowsAreValid(request.Rows);

        var seller = await _dbContext.Sellers.SingleAsync(x => x.Id == request.SellerId, cancellationToken);
        if (seller.DepositStatus != SellerDepositStatuses.Pending)
        {
            throw new InvalidOperationException("Seller deposit is already validated.");
        }

        var nextItemNumber = await _dbContext.Items
            .Where(x => x.SellerId == request.SellerId)
            .Select(x => (int?)x.ItemNumber)
            .MaxAsync(cancellationToken) ?? 0;
        var createdItems = AddItemRows(seller, request.Rows, nextItemNumber);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return createdItems;
    }

    public async Task<UpdateSellerResult> UpdateSellerAsync(long sellerId, UpdateSellerRequest request, CancellationToken cancellationToken = default)
    {
        var seller = await _dbContext.Sellers.SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return UpdateSellerResult.NotFound;
        }

        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken))
        {
            return UpdateSellerResult.NotAllowed;
        }

        if (seller.DepositStatus != SellerDepositStatuses.Pending)
        {
            return UpdateSellerResult.NotAllowed;
        }

        var normalizedPhone = NormalizeOptional(request.Phone) ?? string.Empty;
        var normalizedPostalAddress = NormalizeOptional(request.PostalAddress) ?? string.Empty;
        var normalizedPostalCity = NormalizeSellerPostalCity(request.PostalCode, request.City);
        var normalizedIdentityDocumentType = NormalizeOptional(request.IdentityDocumentType);
        var normalizedIdentityDocumentNumber = NormalizeOptional(request.IdentityDocumentNumber);
        var normalizedIdentityDocumentIssuer = NormalizeOptional(request.IdentityDocumentIssuer);
        EnsureSellerFeePaidIsValid(request.FeePaidCents);

        var duplicateMatches = await _sellerDuplicateService.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest(request.FirstName, request.LastName, normalizedPhone, sellerId),
            cancellationToken);
        if (duplicateMatches.Count > 0)
        {
            return UpdateSellerResult.DuplicateFound(duplicateMatches);
        }

        seller.FirstName = request.FirstName.Trim();
        seller.LastName = request.LastName.Trim();
        seller.Email = NormalizeOptional(request.Email);
        seller.Phone = normalizedPhone;
        seller.PostalAddress = normalizedPostalAddress;
        seller.PostalCode = normalizedPostalCity.PostalCode;
        seller.City = normalizedPostalCity.City;
        seller.IdentityDocumentType = normalizedIdentityDocumentType;
        seller.IdentityDocumentNumber = normalizedIdentityDocumentNumber;
        seller.IdentityDocumentIssuedOn = request.IdentityDocumentIssuedOn;
        seller.IdentityDocumentIssuer = normalizedIdentityDocumentIssuer;
        seller.FeePaidCents = request.FeePaidCents;
        seller.Notes = NormalizeOptional(request.Notes);
        seller.ImportNotes = NormalizeOptional(request.ImportNotes);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return UpdateSellerResult.Updated;
    }

    public async Task<UpdateSellerResult> UpdateSellerFeePaidAsync(
        long sellerId,
        int feePaidCents,
        CancellationToken cancellationToken = default)
    {
        var seller = await _dbContext.Sellers.SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return UpdateSellerResult.NotFound;
        }

        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken)
            || seller.DepositStatus != SellerDepositStatuses.Pending)
        {
            return UpdateSellerResult.NotAllowed;
        }

        EnsureSellerFeePaidIsValid(feePaidCents);
        seller.FeePaidCents = feePaidCents;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return UpdateSellerResult.Updated;
    }

    public async Task<UpdateItemResult> UpdateItemAsync(long itemId, UpdateItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Items
            .Include(x => x.Seller)
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null)
        {
            return UpdateItemResult.NotFound;
        }

        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken)
            || item.Seller.DepositStatus != SellerDepositStatuses.Pending
            || item.Status is not ("registered" or "in_stock"))
        {
            return UpdateItemResult.NotAllowed;
        }

        EnsureSellerPriceIsWholeEuro(request.SellerPriceCents);
        item.Title = request.Title.Trim();
        item.SellerPriceCents = request.SellerPriceCents;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return UpdateItemResult.Updated;
    }

    public async Task<DeleteItemResult> DeleteSellerItemAsync(
        long sellerId,
        long itemId,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Items
            .Include(x => x.Seller)
            .SingleOrDefaultAsync(x => x.Id == itemId && x.SellerId == sellerId, cancellationToken);

        if (item is null)
        {
            return DeleteItemResult.NotFound;
        }

        if (!ItemDeletionPolicy.CanDelete(item.Status)
            || item.Seller.DepositStatus != SellerDepositStatuses.Pending
            || !await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken))
        {
            return DeleteItemResult.NotAllowed;
        }

        var hasHistoricalSaleLine = await _dbContext.SaleLines
            .AnyAsync(x => x.ItemId == itemId, cancellationToken);
        if (hasHistoricalSaleLine)
        {
            return DeleteItemResult.NotAllowed;
        }

        _dbContext.Items.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return DeleteItemResult.Deleted;
    }

    public async Task<SellerDepositStatusChangeResult> ValidateSellerDepositAsync(
        long sellerId,
        CancellationToken cancellationToken = default)
    {
        var seller = await _dbContext.Sellers
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return SellerDepositStatusChangeResult.NotFound;
        }

        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken)
            || seller.DepositStatus != SellerDepositStatuses.Pending
            || seller.Items.Count == 0
            || !SellerCanBeValidated(seller))
        {
            return SellerDepositStatusChangeResult.NotAllowed;
        }

        foreach (var item in seller.Items.Where(x => x.Status == "registered"))
        {
            item.Status = "in_stock";
        }

        seller.DepositStatus = SellerDepositStatuses.Validated;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return SellerDepositStatusChangeResult.Changed;
    }

    public async Task<SellerDepositStatusChangeResult> CancelSellerDepositValidationAsync(
        long sellerId,
        CancellationToken cancellationToken = default)
    {
        var seller = await _dbContext.Sellers
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return SellerDepositStatusChangeResult.NotFound;
        }

        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken)
            || seller.DepositStatus != SellerDepositStatuses.Validated)
        {
            return SellerDepositStatusChangeResult.NotAllowed;
        }

        foreach (var item in seller.Items.Where(x => x.Status == "in_stock"))
        {
            item.Status = "registered";
        }

        seller.DepositStatus = SellerDepositStatuses.Pending;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return SellerDepositStatusChangeResult.Changed;
    }

    public async Task<bool> CanEditItemMasterDataAsync(CancellationToken cancellationToken = default)
    {
        return await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken);
    }

    public async Task<bool> CanEditSellerMasterDataAsync(CancellationToken cancellationToken = default)
    {
        return await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken);
    }

    private async Task<string> AllocateSellerCodeAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _dbContext.Sellers
            .AsNoTracking()
            .Select(x => x.SellerCode)
            .ToListAsync(cancellationToken);

        return SellerCodeGenerator.GetNextAvailableCode(existingCodes);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private SellerPostalCity NormalizeSellerPostalCity(string? postalCode, string? city)
    {
        var normalizedPostalCode = NormalizeOptional(postalCode) ?? string.Empty;
        var normalizedCity = NormalizeOptional(city) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPostalCode) || string.IsNullOrWhiteSpace(normalizedCity))
        {
            return new SellerPostalCity(normalizedPostalCode, normalizedCity);
        }

        if (_postalCityLookup.TryFindCanonicalCity(normalizedPostalCode, normalizedCity, out var exactCity)
            || _postalCityLookup.TryFindCanonicalCityLenient(normalizedPostalCode, normalizedCity, out exactCity))
        {
            return new SellerPostalCity(normalizedPostalCode, exactCity ?? normalizedCity);
        }

        if (_postalCityLookup.HasCitiesForPostalCode(normalizedPostalCode))
        {
            throw new InvalidSellerPostalCityException();
        }

        return new SellerPostalCity(normalizedPostalCode, normalizedCity);
    }

    private static void EnsureSellerPriceIsWholeEuro(int? sellerPriceCents)
    {
        if (sellerPriceCents is null)
        {
            return;
        }

        if (sellerPriceCents % 100 != 0)
        {
            throw new ArgumentException("Seller price must be a whole euro amount.", nameof(sellerPriceCents));
        }
    }

    private static void EnsureSellerFeePaidIsValid(int feePaidCents)
    {
        if (feePaidCents < 0)
        {
            throw new ArgumentException("Seller fee paid must be greater than or equal to zero.", nameof(feePaidCents));
        }

        if (feePaidCents % 100 != 0)
        {
            throw new ArgumentException("Seller fee paid must be a whole euro amount.", nameof(feePaidCents));
        }
    }

    private static void EnsureRowsAreValid(IReadOnlyList<CreateItemRowRequest> rows)
    {
        if (rows.Count == 0)
        {
            throw new ArgumentException("At least one item row is required.", "Rows");
        }

        foreach (var row in rows)
        {
            EnsureSellerPriceIsWholeEuro(row.SellerPriceCents);
        }
    }

    private sealed record SellerPostalCity(string PostalCode, string City);

    private static bool SellerCanBeValidated(SellerEntity seller)
    {
        return !string.IsNullOrWhiteSpace(seller.FirstName)
            && !string.IsNullOrWhiteSpace(seller.LastName)
            && !string.IsNullOrWhiteSpace(seller.Phone)
            && !string.IsNullOrWhiteSpace(seller.PostalAddress)
            && !string.IsNullOrWhiteSpace(seller.PostalCode)
            && !string.IsNullOrWhiteSpace(seller.City)
            && !string.IsNullOrWhiteSpace(seller.IdentityDocumentType)
            && !string.IsNullOrWhiteSpace(seller.IdentityDocumentNumber)
            && seller.IdentityDocumentIssuedOn is not null
            && !string.IsNullOrWhiteSpace(seller.IdentityDocumentIssuer)
            && seller.Items.All(item =>
                (item.Status is "registered" or "in_stock" or "sold" or "missing" or "defective")
                && !string.IsNullOrWhiteSpace(item.Title)
                && item.SellerPriceCents is >= 0);
    }

    private List<ItemEntity> AddItemRows(
        SellerEntity seller,
        IReadOnlyList<CreateItemRowRequest> rows,
        int currentMaxItemNumber)
    {
        var createdItems = new List<ItemEntity>();
        var nextItemNumber = currentMaxItemNumber;

        foreach (var row in rows)
        {
            nextItemNumber++;
            var item = new ItemEntity
            {
                Seller = seller,
                SellerId = seller.Id,
                ItemNumber = nextItemNumber,
                Title = row.Title.Trim(),
                SellerPriceCents = row.SellerPriceCents,
                Status = "registered"
            };

            _dbContext.Items.Add(item);
            createdItems.Add(item);
        }

        return createdItems;
    }

    private static bool IsUniqueConstraintFailure(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteErrorCode == 19;
    }
}
