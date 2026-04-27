using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Pages.Items;
using AssociationSaleTool.Web.Pages.Sellers;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
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
                    new DetailsModel.ItemRowInput { Title = "Toy train", SellerPriceCents = 500 },
                    new DetailsModel.ItemRowInput { Title = "Toy piano", SellerPriceCents = 1000 }
                ]
            }
        };

        var result = await pageModel.OnPostAsync(seller.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal(2, await context.Items.CountAsync());
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
                SellerPriceCents = 1200
            }
        };

        var result = await pageModel.OnPostAsync(item.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sellers/Details", redirect.PageName);
        Assert.Equal("Concert toy piano", (await context.Items.SingleAsync()).Title);
    }
}
