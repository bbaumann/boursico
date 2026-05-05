using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Pages.Items;
using AssociationSaleTool.Web.Pages.Sellers;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AssociationSaleTool.Web.Tests.Pages;

using ItemEditModel = AssociationSaleTool.Web.Pages.Items.EditModel;
using SellerEditModel = AssociationSaleTool.Web.Pages.Sellers.EditModel;

public sealed class SellersPageModelTests
{
    [Fact]
    public void SellerIndexPage_BuildSellerFilterText_IncludesCodeAndName()
    {
        var seller = new AssociationSaleTool.Web.Data.Entities.SellerEntity
        {
            SellerCode = "SL-101",
            FirstName = "Ariane",
            LastName = "Martin"
        };

        var filterText = IndexModel.BuildSellerFilterText(seller);

        Assert.Equal("sl-101 ariane martin", filterText);
    }

    [Fact]
    public void SellerCreateAndEditInputsRequirePhoneAddressPostalCodeAndCity()
    {
        AssertRequiredContactMessages(new CreateModel.InputModel
        {
            FirstName = "Ada",
            LastName = "Lovelace"
        });

        AssertRequiredContactMessages(new SellerEditModel.InputModel
        {
            FirstName = "Ada",
            LastName = "Lovelace"
        });
    }

    [Fact]
    public async Task CreateSellerPage_Post_CreatesSellerAndRedirectsToDetails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);

        var pageModel = new CreateModel(service)
        {
            Input = new CreateModel.InputModel
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                Email = "ada@example.com",
                Phone = "0123",
                PostalAddress = "1 rue des Tests"
            }
        };

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal("BB", (await context.Sellers.SingleAsync()).SellerCode);
    }

    [Fact]
    public async Task CreateSellerPage_Post_StoresSellerFeePaid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);

        var pageModel = new CreateModel(service)
        {
            Input = new CreateModel.InputModel
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                FeePaid = "2"
            }
        };

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(200, (await context.Sellers.SingleAsync()).FeePaidCents);
    }

    [Fact]
    public async Task CreateSellerPage_Post_ReturnsPageWithDuplicateMatches()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var existing = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", "ada@example.com", "0123", "1 rue des Tests", null))).Seller!;

        var pageModel = new CreateModel(service)
        {
            Input = new CreateModel.InputModel
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                Phone = "0123",
                PostalAddress = "2 rue des Tests"
            }
        };

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        var match = Assert.Single(pageModel.DuplicateMatches);
        Assert.Equal(existing.Id, match.SellerId);
        Assert.Equal("ada@example.com", match.Email);
        Assert.Equal(1, await context.Sellers.CountAsync());
    }

    [Fact]
    public async Task SellerDetailsPage_Post_CreatesMultipleItemsAndRedirectsToSelf()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var pageModel = new DetailsModel(service, new DefectiveItemService(context))
        {
            Input = new DetailsModel.ItemBatchInput
            {
                IntakeMode = IntakeMode.InPerson,
                Rows =
                [
                    new DetailsModel.ItemRowInput { Title = "Toy train", SellerPrice = "4" },
                    new DetailsModel.ItemRowInput { Title = "Toy piano", SellerPrice = "10" }
                ]
            }
        };

        var result = await pageModel.OnPostAsync(seller.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal(2, await context.Items.CountAsync());
        Assert.Collection(
            await context.Items.OrderBy(x => x.ItemNumber).ToListAsync(),
            first => Assert.Equal(400, first.SellerPriceCents),
            second => Assert.Equal(1000, second.SellerPriceCents));
    }

    [Fact]
    public async Task SellerDetailsPage_Get_DefaultsToEmailPreregistrationWithTenRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var pageModel = new DetailsModel(service, new DefectiveItemService(context));

        var result = await pageModel.OnGetAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(IntakeMode.EmailPreregistration, pageModel.Input.IntakeMode);
        Assert.Equal(10, pageModel.Input.Rows.Count);
    }

    [Fact]
    public async Task SellerDetailsPage_Get_ComputesSellerFeeStatusFromTotalItemCount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null, 200))).Seller!;
        var rows = Enumerable.Range(1, 16)
            .Select(index => new CreateItemRowRequest($"Toy {index}", 100))
            .ToList();
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(seller.Id, IntakeMode.EmailPreregistration, rows));
        var pageModel = new DetailsModel(service, new DefectiveItemService(context));

        var result = await pageModel.OnGetAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(400, pageModel.FeeStatus.ExpectedFeeCents);
        Assert.Equal(200, pageModel.FeeStatus.PaidFeeCents);
        Assert.Equal(200, pageModel.FeeStatus.RemainingFeeCents);
        Assert.False(pageModel.FeeStatus.IsFullyPaid);
    }

    [Fact]
    public async Task SellerEditPage_Post_UpdatesSellerAndRedirectsToDetails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var pageModel = new SellerEditModel(service)
        {
            Input = new SellerEditModel.InputModel
            {
                FirstName = "Augusta",
                LastName = "King",
                Email = "augusta@example.com",
                Phone = "1234",
                PostalAddress = "10 avenue des Jouets",
                Notes = "Updated"
            }
        };

        var result = await pageModel.OnPostAsync(seller.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal("Augusta", (await context.Sellers.SingleAsync()).FirstName);
    }

    [Fact]
    public async Task SellerEditPage_Post_UpdatesSellerFeePaid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var pageModel = new SellerEditModel(service)
        {
            Input = new SellerEditModel.InputModel
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                FeePaid = "4"
            }
        };

        var result = await pageModel.OnPostAsync(seller.Id, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(400, (await context.Sellers.SingleAsync()).FeePaidCents);
    }

    [Fact]
    public async Task SellerEditPage_Post_ReturnsPageForNegativeSellerFeePaid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var pageModel = new SellerEditModel(service)
        {
            Input = new SellerEditModel.InputModel
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                FeePaid = "-1"
            }
        };

        var result = await pageModel.OnPostAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Contains("supérieurs ou égaux à zéro", pageModel.ModelState["Input.FeePaid"]!.Errors.Single().ErrorMessage);
        Assert.Equal(0, (await context.Sellers.SingleAsync()).FeePaidCents);
    }

    [Fact]
    public async Task SellerEditPage_Post_ReturnsPageWithDuplicateMatches()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var existing = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", "ada@example.com", "0123", "1 rue des Tests", null))).Seller!;
        var edited = (await service.CreateSellerAsync(new CreateSellerRequest("Grace", "Hopper", null, "9999", "2 rue des Tests", null))).Seller!;

        var pageModel = new SellerEditModel(service)
        {
            Input = new SellerEditModel.InputModel
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                Phone = "0123",
                PostalAddress = "3 rue des Tests"
            }
        };

        var result = await pageModel.OnPostAsync(edited.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        var match = Assert.Single(pageModel.DuplicateMatches);
        Assert.Equal(existing.Id, match.SellerId);
    }

    [Fact]
    public async Task SellerEditPage_Post_ReturnsPageWhenSellerMasterDataIsLocked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var phaseService = new SalePhaseService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        await phaseService.SetPhaseAsync(SalePhase.Sale);

        var pageModel = new SellerEditModel(service)
        {
            Input = new SellerEditModel.InputModel
            {
                FirstName = "Augusta",
                LastName = "King",
                Email = "augusta@example.com",
                Phone = "1234",
                PostalAddress = "10 avenue des Jouets",
                Notes = "Updated"
            }
        };

        var result = await pageModel.OnPostAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.False(pageModel.CanEditSellerMasterData);
        var unchangedSeller = await context.Sellers.SingleAsync();
        Assert.Equal("Ada", unchangedSeller.FirstName);
        Assert.Equal("Lovelace", unchangedSeller.LastName);
    }

    [Fact]
    public async Task ItemEditPage_Post_UpdatesItemAndRedirectsToSellerDetails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        var pageModel = new ItemEditModel(service)
        {
            Input = new ItemEditModel.InputModel
            {
                Title = "Concert toy piano",
                SellerPrice = "12"
            }
        };

        var result = await pageModel.OnPostAsync(item.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal("Concert toy piano", (await context.Items.SingleAsync()).Title);
        Assert.Equal(1200, (await context.Items.SingleAsync()).SellerPriceCents);
    }

    [Fact]
    public async Task SellerDetailsPage_Post_ReturnsPageForSellerPriceWithCents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var pageModel = new DetailsModel(service, new DefectiveItemService(context))
        {
            Input = new DetailsModel.ItemBatchInput
            {
                IntakeMode = IntakeMode.InPerson,
                Rows = [new DetailsModel.ItemRowInput { Title = "Rounded toy", SellerPrice = "4,50" }]
            }
        };

        var result = await pageModel.OnPostAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Contains("entier", pageModel.ModelState["Input.Rows[0].SellerPrice"]!.Errors.Single().ErrorMessage);
        Assert.Empty(await context.Items.ToListAsync());
    }

    [Fact]
    public async Task ItemEditPage_Post_ReturnsPageForInvalidEuroPrice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        var pageModel = new ItemEditModel(service)
        {
            Input = new ItemEditModel.InputModel
            {
                Title = "Concert toy piano",
                SellerPrice = "abc"
            }
        };

        var result = await pageModel.OnPostAsync(item.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
    }

    [Fact]
    public async Task ItemEditPage_Post_ReturnsPageForSellerPriceWithCents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        var pageModel = new ItemEditModel(service)
        {
            Input = new ItemEditModel.InputModel
            {
                Title = "Concert toy piano",
                SellerPrice = "12.50"
            }
        };

        var result = await pageModel.OnPostAsync(item.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Contains("entier", pageModel.ModelState["Input.SellerPrice"]!.Errors.Single().ErrorMessage);
        Assert.Equal(1000, (await context.Items.SingleAsync()).SellerPriceCents);
    }

    [Fact]
    public async Task SellerDetailsPage_PostDeleteItem_DeletesAllowedItemAndRedirectsToSelf()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        var pageModel = new DetailsModel(service, new DefectiveItemService(context));

        var result = await pageModel.OnPostDeleteItemAsync(seller.Id, item.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Empty(await context.Items.ToListAsync());
    }

    [Fact]
    public async Task SellerDetailsPage_PostDeleteItem_ReturnsPageForDisallowedStatus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = new AssociationSaleTool.Web.Data.Entities.ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-001",
            ItemNumber = 1,
            Title = "Toy piano",
            SellerPriceCents = 1000,
            Status = "sold"
        };

        context.Items.Add(item);
        await context.SaveChangesAsync();

        var pageModel = new DetailsModel(service, new DefectiveItemService(context));

        var result = await pageModel.OnPostDeleteItemAsync(seller.Id, item.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Equal("sold", (await context.Items.SingleAsync()).Status);
    }

    [Fact]
    public async Task SellerDetailsPage_PostMarkItemInStock_ChangesRegisteredItemAndRedirectsToSelf()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        var pageModel = new DetailsModel(service, new DefectiveItemService(context));

        var result = await pageModel.OnPostMarkItemInStockAsync(seller.Id, item.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal("in_stock", (await context.Items.SingleAsync()).Status);
    }

    [Fact]
    public async Task SellerDetailsPage_PostMarkItemInStock_ReturnsPageForDisallowedStatus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        var pageModel = new DetailsModel(service, new DefectiveItemService(context));

        var result = await pageModel.OnPostMarkItemInStockAsync(seller.Id, item.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Equal("in_stock", (await context.Items.SingleAsync()).Status);
    }

    private static void AssertRequiredContactMessages(object input)
    {
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            validationResults,
            validateAllProperties: true);

        Assert.Contains(validationResults, result => result.ErrorMessage == "Le téléphone est obligatoire.");
        Assert.Contains(validationResults, result => result.ErrorMessage == "L'adresse postale est obligatoire.");
        Assert.Contains(validationResults, result => result.ErrorMessage == "Le code postal est obligatoire.");
        Assert.Contains(validationResults, result => result.ErrorMessage == "La ville est obligatoire.");
    }
}
