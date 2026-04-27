using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Pages.Items;
using AssociationSaleTool.Web.Pages.Sellers;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Pages;

using ItemEditModel = AssociationSaleTool.Web.Pages.Items.EditModel;
using SellerEditModel = AssociationSaleTool.Web.Pages.Sellers.EditModel;

public sealed class SellersPageModelTests
{
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
                Email = "ada@example.com"
            }
        };

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal("AA", (await context.Sellers.SingleAsync()).SellerCode);
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
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));

        var pageModel = new DetailsModel(service)
        {
            Input = new DetailsModel.ItemBatchInput
            {
                IntakeMode = IntakeMode.InPerson,
                Rows =
                [
                    new DetailsModel.ItemRowInput { Title = "Toy train", SellerPrice = "4,50" },
                    new DetailsModel.ItemRowInput { Title = "Toy piano", SellerPrice = "10.00" }
                ]
            }
        };

        var result = await pageModel.OnPostAsync(seller.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal(2, await context.Items.CountAsync());
        Assert.Collection(
            await context.Items.OrderBy(x => x.ItemNumber).ToListAsync(),
            first => Assert.Equal(450, first.SellerPriceCents),
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
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));
        var pageModel = new DetailsModel(service);

        var result = await pageModel.OnGetAsync(seller.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(IntakeMode.EmailPreregistration, pageModel.Input.IntakeMode);
        Assert.Equal(10, pageModel.Input.Rows.Count);
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
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));

        var pageModel = new SellerEditModel(service)
        {
            Input = new SellerEditModel.InputModel
            {
                FirstName = "Augusta",
                LastName = "King",
                Email = "augusta@example.com",
                Phone = "1234",
                Notes = "Updated"
            }
        };

        var result = await pageModel.OnPostAsync(seller.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal("Augusta", (await context.Sellers.SingleAsync()).FirstName);
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
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        var pageModel = new ItemEditModel(service)
        {
            Input = new ItemEditModel.InputModel
            {
                Title = "Concert toy piano",
                SellerPrice = "12,00"
            }
        };

        var result = await pageModel.OnPostAsync(item.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal("Concert toy piano", (await context.Items.SingleAsync()).Title);
        Assert.Equal(1200, (await context.Items.SingleAsync()).SellerPriceCents);
    }

    [Fact]
    public async Task SellerDetailsPage_Post_RoundsEuroInputToTwoDecimals()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));

        var pageModel = new DetailsModel(service)
        {
            Input = new DetailsModel.ItemBatchInput
            {
                IntakeMode = IntakeMode.InPerson,
                Rows = [new DetailsModel.ItemRowInput { Title = "Rounded toy", SellerPrice = "4.555" }]
            }
        };

        var result = await pageModel.OnPostAsync(seller.Id, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(456, (await context.Items.SingleAsync()).SellerPriceCents);
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
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));
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
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        var pageModel = new DetailsModel(service);

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
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));
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

        var pageModel = new DetailsModel(service);

        var result = await pageModel.OnPostDeleteItemAsync(seller.Id, item.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Equal("sold", (await context.Items.SingleAsync()).Status);
    }
}
