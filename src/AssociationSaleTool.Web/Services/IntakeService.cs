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

    public IntakeService(
        AppDbContext dbContext,
        SalePhaseService? salePhaseService = null,
        SellerDuplicateService? sellerDuplicateService = null)
    {
        _dbContext = dbContext;
        _salePhaseService = salePhaseService ?? new SalePhaseService(dbContext);
        _sellerDuplicateService = sellerDuplicateService ?? new SellerDuplicateService(dbContext);
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
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
    }

    public async Task<CreateSellerResult> CreateSellerAsync(CreateSellerRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken))
        {
            throw new InvalidOperationException("Seller master data is locked outside presale.");
        }

        var normalizedPhone = NormalizeRequired(request.Phone, nameof(request.Phone));
        var normalizedPostalAddress = NormalizeRequired(request.PostalAddress, nameof(request.PostalAddress));
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
            SellerCode = SellerCodeGenerator.GetCodeAt(await _dbContext.Sellers.CountAsync(cancellationToken)),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = NormalizeOptional(request.Email),
            Phone = normalizedPhone,
            PostalAddress = normalizedPostalAddress,
            FeePaidCents = request.FeePaidCents,
            Notes = NormalizeOptional(request.Notes)
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

        var normalizedPhone = NormalizeRequired(request.Phone, nameof(request.Phone));
        var normalizedPostalAddress = NormalizeRequired(request.PostalAddress, nameof(request.PostalAddress));
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
                    SellerCode = SellerCodeGenerator.GetCodeAt(await _dbContext.Sellers.CountAsync(cancellationToken)),
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = NormalizeOptional(request.Email),
                    Phone = normalizedPhone,
                    PostalAddress = normalizedPostalAddress,
                    FeePaidCents = request.FeePaidCents,
                    Notes = NormalizeOptional(request.Notes)
                };

                _dbContext.Sellers.Add(seller);
                AddItemRows(seller, request.IntakeMode, request.Rows, 0);

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
        var nextItemNumber = await _dbContext.Items
            .Where(x => x.SellerId == request.SellerId)
            .Select(x => (int?)x.ItemNumber)
            .MaxAsync(cancellationToken) ?? 0;
        var createdItems = AddItemRows(seller, request.IntakeMode, request.Rows, nextItemNumber);

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

        var normalizedPhone = NormalizeRequired(request.Phone, nameof(request.Phone));
        var normalizedPostalAddress = NormalizeRequired(request.PostalAddress, nameof(request.PostalAddress));
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
        seller.FeePaidCents = request.FeePaidCents;
        seller.Notes = NormalizeOptional(request.Notes);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return UpdateSellerResult.Updated;
    }

    public async Task<UpdateItemResult> UpdateItemAsync(long itemId, UpdateItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Items.SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null)
        {
            return UpdateItemResult.NotFound;
        }

        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken)
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
            .SingleOrDefaultAsync(x => x.Id == itemId && x.SellerId == sellerId, cancellationToken);

        if (item is null)
        {
            return DeleteItemResult.NotFound;
        }

        if (!ItemDeletionPolicy.CanDelete(item.Status) || !await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken))
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

    public async Task<ItemStatusChangeResult> MarkSellerItemInStockAsync(
        long sellerId,
        long itemId,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Items
            .SingleOrDefaultAsync(x => x.Id == itemId && x.SellerId == sellerId, cancellationToken);

        if (item is null)
        {
            return ItemStatusChangeResult.NotFound;
        }

        if (item.Status != "registered" || !await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken))
        {
            return ItemStatusChangeResult.NotAllowed;
        }

        item.Status = "in_stock";
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ItemStatusChangeResult.Changed;
    }

    public async Task<bool> CanEditItemMasterDataAsync(CancellationToken cancellationToken = default)
    {
        return await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken);
    }

    public async Task<bool> CanEditSellerMasterDataAsync(CancellationToken cancellationToken = default)
    {
        return await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRequired(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Required seller contact information is missing.", paramName);
        }

        return value.Trim();
    }

    private static void EnsureSellerPriceIsWholeEuro(int sellerPriceCents)
    {
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

    private List<ItemEntity> AddItemRows(
        SellerEntity seller,
        IntakeMode intakeMode,
        IReadOnlyList<CreateItemRowRequest> rows,
        int currentMaxItemNumber)
    {
        var status = intakeMode == IntakeMode.EmailPreregistration ? "registered" : "in_stock";
        var createdItems = new List<ItemEntity>();
        var nextItemNumber = currentMaxItemNumber;

        foreach (var row in rows)
        {
            nextItemNumber++;
            var item = new ItemEntity
            {
                Seller = seller,
                SellerId = seller.Id,
                Reference = ItemReference.Format(seller.SellerCode, nextItemNumber),
                ItemNumber = nextItemNumber,
                Title = row.Title.Trim(),
                SellerPriceCents = row.SellerPriceCents,
                Status = status
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
