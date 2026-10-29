using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Pages.Items;
using AssociationSaleTool.Web.Pages.Sellers;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace AssociationSaleTool.Web.Tests.Pages;

using ItemEditModel = AssociationSaleTool.Web.Pages.Items.EditModel;
using SellerEditModel = AssociationSaleTool.Web.Pages.Sellers.EditModel;

public sealed class SellersPageModelTests
{
    [Fact]
    public void SellerIndexPage_BuildSellerFilterText_IncludesNameOnly()
    {
        var seller = new AssociationSaleTool.Web.Data.Entities.SellerEntity
        {
            SellerCode = "SL-101",
            FirstName = "Ariane",
            LastName = "Martin"
        };

        var filterText = IndexModel.BuildSellerFilterText(seller);

        Assert.Equal("ariane martin", filterText);
    }

    [Fact]
    public void SellerCreateAndEditInputsAllowMissingContactFieldsBeforeDepositValidation()
    {
        AssertContactFieldsAreOptional(new CreateModel.InputModel
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            IdentityDocumentType = string.Empty,
            IdentityDocumentNumber = string.Empty,
            IdentityDocumentIssuedOn = null
        });

        AssertContactFieldsAreOptional(new SellerEditModel.InputModel
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            IdentityDocumentType = string.Empty,
            IdentityDocumentNumber = string.Empty,
            IdentityDocumentIssuedOn = null
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
                PostalAddress = "1 rue des Tests",
                IntakeMode = IntakeMode.InPerson,
                Rows = [new CreateModel.ItemRowInput { Title = "Toy train", SellerPrice = "4" }]
            }
        };

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal("BB", (await context.Sellers.SingleAsync()).SellerCode);
        var item = await context.Items.SingleAsync();
        Assert.Null(item.Reference);
        Assert.Equal("registered", item.Status);
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
                FeePaid = "2",
                IntakeMode = IntakeMode.EmailPreregistration,
                Rows = [new CreateModel.ItemRowInput { Title = "Toy train", SellerPrice = "4" }]
            }
        };

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(200, (await context.Sellers.SingleAsync()).FeePaidCents);
    }

    [Fact]
    public async Task CreateSellerPage_Post_RedirectsDuplicateSellerWithPendingRows()
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
                PostalAddress = "2 rue des Tests",
                IntakeMode = IntakeMode.EmailPreregistration,
                Rows = [new CreateModel.ItemRowInput { Title = "Toy train", SellerPrice = "4" }]
            }
        };
        var session = new TestSession();
        pageModel.PageContext = new PageContext { HttpContext = CreateHttpContextWithSession(session) };

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal(existing.Id, redirect.RouteValues!["id"]);
        var pendingRows = PendingSellerItemRowsSession.Consume(session, existing.Id);
        Assert.NotNull(pendingRows);
        Assert.Equal(IntakeMode.EmailPreregistration, pendingRows.IntakeMode);
        var row = Assert.Single(pendingRows.Rows);
        Assert.Equal("Toy train", row.Title);
        Assert.Equal(400, row.SellerPriceCents);
        Assert.Equal(1, await context.Sellers.CountAsync());
    }

    [Fact]
    public async Task CreateSellerPage_Post_BlocksNameOnlyDuplicateWhenPhoneIsMissing()
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
                Phone = string.Empty,
                PostalAddress = string.Empty,
                IntakeMode = IntakeMode.EmailPreregistration,
                Rows = [new CreateModel.ItemRowInput { Title = "Toy train", SellerPrice = "4" }]
            }
        };
        var session = new TestSession();
        pageModel.PageContext = new PageContext { HttpContext = CreateHttpContextWithSession(session) };

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal(existing.Id, redirect.RouteValues!["id"]);
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

        var pageModel = new DetailsModel(service, new DepositDeskService(context), context)
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
            await context.Items.OrderBy(x => x.Id).ToListAsync(),
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
        var pageModel = new DetailsModel(service, new DepositDeskService(context), context);

        var result = await pageModel.OnGetAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(IntakeMode.EmailPreregistration, pageModel.Input.IntakeMode);
        Assert.Equal(5, pageModel.Input.Rows.Count);
    }

    [Fact]
    public async Task SellerDetailsPage_Get_ConsumesPendingRowsOnce()
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
        var session = new TestSession();
        PendingSellerItemRowsSession.Store(
            session,
            new PendingSellerItemRows(
                seller.Id,
                IntakeMode.EmailPreregistration,
                [new CreateItemRowRequest("Toy train", 400)],
                PendingSellerItemRowsSession.ExcelDuplicateAlertKey));
        var pageModel = new DetailsModel(service, new DepositDeskService(context), context)
        {
            PageContext = new PageContext { HttpContext = CreateHttpContextWithSession(session) }
        };

        var result = await pageModel.OnGetAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Ce vendeur existe déjà. Les articles saisis ont été ajoutés au formulaire ci-dessous. Vérifiez puis enregistrez le lot.", pageModel.PendingRowsAlert);
        Assert.Equal(IntakeMode.EmailPreregistration, pageModel.Input.IntakeMode);
        Assert.Equal("Toy train", pageModel.Input.Rows[0].Title);
        Assert.Equal("4", pageModel.Input.Rows[0].SellerPrice);
        Assert.True(pageModel.Input.Rows.Count >= 5);

        var secondResult = await pageModel.OnGetAsync(seller.Id, CancellationToken.None);
        Assert.IsType<PageResult>(secondResult);
        Assert.Null(pageModel.PendingRowsAlert);
        Assert.True(pageModel.Input.Rows.All(row => string.IsNullOrWhiteSpace(row.Title)));
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
        var pageModel = new DetailsModel(service, new DepositDeskService(context), context);

        var result = await pageModel.OnGetAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(400, pageModel.FeeStatus.ExpectedFeeCents);
        Assert.Equal(200, pageModel.FeeStatus.PaidFeeCents);
        Assert.Equal(200, pageModel.FeeStatus.RemainingFeeCents);
        Assert.False(pageModel.FeeStatus.IsFullyPaid);
    }

    [Fact]
    public async Task SellerDetailsPage_PostUpdateDepositFeePaid_UpdatesFeeAndReopensPrintStep()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = (await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)))).Seller!;
        var rows = Enumerable.Range(1, 16)
            .Select(index => new CreateItemRowRequest($"Toy {index}", 100))
            .ToList();
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(seller.Id, IntakeMode.EmailPreregistration, rows));
        await service.StartDepositAsync(seller.Id, (await new DepositDeskService(context).OpenDeskAsync()).DeskId);
        var pageModel = new DetailsModel(service, new DepositDeskService(context), context)
        {
            DepositFeeInput = new DetailsModel.DepositFeeInputModel
            {
                FeePaid = "4"
            }
        };

        var result = await pageModel.OnPostUpdateDepositFeeAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ShouldOpenDepositFinalizationDialog);
        Assert.Equal("print", pageModel.DepositFinalizationInitialStep);
        Assert.Equal(400, pageModel.FeeStatus.ExpectedFeeCents);
        Assert.Equal(400, pageModel.FeeStatus.PaidFeeCents);
        Assert.Equal(0, pageModel.FeeStatus.RemainingFeeCents);
        Assert.Equal(400, (await context.Sellers.SingleAsync()).FeePaidCents);
    }

    [Fact]
    public async Task SellerDetailsPage_PostUpdateDepositFeePaid_ReturnsFeeStepForInvalidAmount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = (await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)))).Seller!;
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy", 100)]));
        await service.StartDepositAsync(seller.Id, (await new DepositDeskService(context).OpenDeskAsync()).DeskId);
        var pageModel = new DetailsModel(service, new DepositDeskService(context), context)
        {
            DepositFeeInput = new DetailsModel.DepositFeeInputModel
            {
                FeePaid = "-1"
            }
        };

        var result = await pageModel.OnPostUpdateDepositFeeAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ShouldOpenDepositFinalizationDialog);
        Assert.Equal("fees", pageModel.DepositFinalizationInitialStep);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Contains("supérieurs ou égaux à zéro", pageModel.ModelState["DepositFeeInput.FeePaid"]!.Errors.Single().ErrorMessage);
        Assert.Equal(0, (await context.Sellers.SingleAsync()).FeePaidCents);
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
    public async Task SellerEditPage_Post_BlocksNameOnlyDuplicateWhenPhoneIsMissing()
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
                Phone = string.Empty,
                PostalAddress = "3 rue des Tests"
            }
        };

        var result = await pageModel.OnPostAsync(edited.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Equal(existing.Id, Assert.Single(pageModel.DuplicateMatches).SellerId);
        var message = Assert.Single(pageModel.ModelState[string.Empty]!.Errors).ErrorMessage;
        Assert.Contains("mêmes nom et prénom", message);
        Assert.Contains("Aucun téléphone n'a été saisi", message);
        Assert.Equal("Grace", (await context.Sellers.SingleAsync(x => x.Id == edited.Id)).FirstName);
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
            [new CreateItemRowRequest("Toy piano", 1000)]))).Items.Single();

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

        var pageModel = new DetailsModel(service, new DepositDeskService(context), context)
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
            [new CreateItemRowRequest("Toy piano", 1000)]))).Items.Single();

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
            [new CreateItemRowRequest("Toy piano", 1000)]))).Items.Single();

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
            [new CreateItemRowRequest("Toy piano", 1000)]))).Items.Single();

        var pageModel = new DetailsModel(service, new DepositDeskService(context), context);

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
            Title = "Toy piano",
            SellerPriceCents = 1000,
            Status = "sold"
        };

        context.Items.Add(item);
        await context.SaveChangesAsync();

        var pageModel = new DetailsModel(service, new DepositDeskService(context), context);

        var result = await pageModel.OnPostDeleteItemAsync(seller.Id, item.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Equal("sold", (await context.Items.SingleAsync()).Status);
    }

    [Fact]
    public async Task SellerDetailsPage_PostValidateDeposit_ChangesRegisteredItemsAndRedirectsToSelf()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Items.Single();
        await service.StartDepositAsync(seller.Id, (await new DepositDeskService(context).OpenDeskAsync()).DeskId);

        var pageModel = new DetailsModel(service, new DepositDeskService(context), context);

        var result = await pageModel.OnPostValidateDepositAsync(seller.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal("in_stock", (await context.Items.SingleAsync()).Status);
        Assert.Equal("deposit_validated", (await context.Sellers.SingleAsync()).DepositStatus);
    }

    [Fact]
    public void SellerDetailsPage_AllocationMessage_ListsASplitRunAndEachTakenLetter()
    {
        var single = new DepositAllocationResult(DepositAllocationStatus.Done, [], ["B007"], []);
        var range = new DepositAllocationResult(DepositAllocationStatus.Done, [], ["B007", "B008", "B009"], []);
        var split = new DepositAllocationResult(DepositAllocationStatus.Done, [], ["B019", "B020", "R001"], ['R']);
        var none = new DepositAllocationResult(DepositAllocationStatus.Done, [], [], []);

        Assert.Equal(
            "1 jouet étiqueté : B007. Décollez l'étiquette suivante de la planche B et collez-la sur le jouet surligné ci-dessous.",
            DetailsModel.ToAllocationMessage(single));
        Assert.Equal(
            "3 jouets étiquetés : B007 à B009. Décollez les étiquettes de la planche B dans l'ordre et collez-les sur les jouets surlignés ci-dessous.",
            DetailsModel.ToAllocationMessage(range));
        Assert.Equal(
            "3 jouets étiquetés : B019, B020, R001. Allez chercher la planche R. Décollez les étiquettes des planches B et R dans l'ordre et collez-les sur les jouets surlignés ci-dessous.",
            DetailsModel.ToAllocationMessage(split));
        Assert.Equal(
            "Dépôt commencé : aucun jouet à étiqueter pour l'instant. Chaque jouet ajouté recevra son étiquette à l'enregistrement.",
            DetailsModel.ToAllocationMessage(none));
    }

    [Theory]
    [InlineData(DeskState.Open)]
    [InlineData(DeskState.Closed)]
    [InlineData(DeskState.None)]
    public void SellerDetailsPage_ToAllocationError_ReportsAClosedDeskWhateverThePageLoadSaw(DeskState deskState)
    {
        // The desk can be closed between page load and save: the cookie still
        // shows it open, but the save refused it as closed.
        Assert.Equal(
            "Les lettres de cet ordinateur ont été libérées. Reprenez des lettres pour continuer.",
            DetailsModel.ToAllocationError(DepositAllocationStatus.DeskClosed, deskState, "refusé"));
    }

    [Theory]
    [InlineData(DeskState.Closed, "Les lettres de cet ordinateur ont été libérées. Reprenez des lettres pour continuer.")]
    [InlineData(DeskState.None, "Prenez des lettres pour attribuer des références.")]
    public void SellerDetailsPage_ToAllocationError_ReportsNoDeskFromTheCookieState(DeskState deskState, string expected)
    {
        Assert.Equal(expected, DetailsModel.ToAllocationError(DepositAllocationStatus.NoDesk, deskState, "refusé"));
    }

    [Fact]
    public async Task SellerDetailsPage_PostValidateDeposit_ReportsANotStartedDepositInsteadOfTheGenericMessage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));

        // Deliberately skip StartDepositAsync: the seller and item data are
        // otherwise complete, so the only reason validation can refuse is
        // that the deposit was never started and has no references.
        var pageModel = new DetailsModel(service, new DepositDeskService(context), context);

        var result = await pageModel.OnPostValidateDepositAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            ["Étiquetez les jouets avant de finaliser le dépôt."],
            pageModel.DepositValidationMessages);

        // Neither the seller-record nor the items are actually incomplete
        // here - the deposit is only not started - so neither blocker
        // summary line ("Fiche vendeur" / "Articles") should show, and the
        // "Corriger les articles incomplets" link (which points at data that
        // isn't the problem) must not appear either. Only the specific
        // message above should be visible to the operator.
        Assert.False(pageModel.HasSellerDepositValidationMessages);
        Assert.False(pageModel.HasItemDepositValidationMessages);
        Assert.Equal("registered", (await context.Items.SingleAsync()).Status);
        Assert.Equal("deposit_pending", (await context.Sellers.SingleAsync()).DepositStatus);
    }

    [Fact]
    public async Task SellerDetailsPage_PostValidateDeposit_ReturnsPageForInvalidSeller()
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
            [new CreateItemRowRequest("Toy piano", 1000)]))).Items.Single();

        var pageModel = new DetailsModel(service, new DepositDeskService(context), context);

        var result = await pageModel.OnPostValidateDepositAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        var errors = pageModel.ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .ToList();
        Assert.Contains("Le code postal est obligatoire.", errors);
        Assert.Contains("La ville est obligatoire.", errors);
        Assert.Contains("La nature de la pièce d'identité est obligatoire.", errors);
        Assert.Contains("Le numéro de la pièce d'identité est obligatoire.", errors);
        Assert.Contains("La date de délivrance est obligatoire.", errors);
        Assert.DoesNotContain("L'autorité de délivrance est obligatoire.", errors);
        Assert.Equal("registered", (await context.Items.SingleAsync()).Status);
        Assert.Equal("deposit_pending", (await context.Sellers.SingleAsync()).DepositStatus);
    }

    private static void AssertContactFieldsAreOptional(object input)
    {
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            validationResults,
            validateAllProperties: true);

        Assert.DoesNotContain(validationResults, result => result.ErrorMessage == "Le téléphone est obligatoire.");
        Assert.DoesNotContain(validationResults, result => result.ErrorMessage == "L'adresse postale est obligatoire.");
        Assert.DoesNotContain(validationResults, result => result.ErrorMessage == "Le code postal est obligatoire.");
        Assert.DoesNotContain(validationResults, result => result.ErrorMessage == "La ville est obligatoire.");
        Assert.DoesNotContain(validationResults, result => result.ErrorMessage == "La nature de la pièce d'identité est obligatoire.");
        Assert.DoesNotContain(validationResults, result => result.ErrorMessage == "Le numéro de la pièce d'identité est obligatoire.");
        Assert.DoesNotContain(validationResults, result => result.ErrorMessage == "La date de délivrance est obligatoire.");
    }

    private static DefaultHttpContext CreateHttpContextWithSession(ISession session)
    {
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new TestSessionFeature { Session = session });
        return context;
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = null!;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];

        public IEnumerable<string> Keys => _values.Keys;

        public string Id { get; } = Guid.NewGuid().ToString("N");

        public bool IsAvailable => true;

        public void Clear()
        {
            _values.Clear();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _values.Remove(key);
        }

        public void Set(string key, byte[] value)
        {
            _values[key] = value;
        }

        public bool TryGetValue(string key, out byte[] value)
        {
            return _values.TryGetValue(key, out value!);
        }
    }
}
