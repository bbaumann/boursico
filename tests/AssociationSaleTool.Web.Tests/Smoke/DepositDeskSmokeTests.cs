using System.Net;
using System.Text.RegularExpressions;
using AssociationSaleTool.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AssociationSaleTool.Web.Tests.Smoke;

public sealed class DepositDeskSmokeTests(IntakeWebApplicationFactory factory) : IClassFixture<IntakeWebApplicationFactory>
{
    private static async Task<string> AntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        return Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    private HttpClient CreateClient()
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
    }

    private static async Task<HttpResponseMessage> PostDeskAsync(HttpClient client, string handler, Dictionary<string, string>? fields = null)
    {
        var form = new Dictionary<string, string>(fields ?? [])
        {
            ["__RequestVerificationToken"] = await AntiforgeryTokenAsync(client, "/vendeurs/poste")
        };
        return await client.PostAsync($"/vendeurs/poste?handler={handler}", new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task SellerPagesShowThatNoDeskIsOpen()
    {
        var client = CreateClient();

        var content = WebUtility.HtmlDecode(await client.GetStringAsync("/vendeurs"));

        Assert.Contains("Aucun poste de dépôt ouvert sur cet ordinateur.", content);
        Assert.Contains("href=\"/vendeurs/poste\"", content);
    }

    [Fact]
    public async Task OpeningADeskShowsItsLettersInTheBannerAndClosingItRemovesTheCookie()
    {
        var client = CreateClient();
        var closed = false;

        try
        {
            var opened = await PostDeskAsync(client, "Open", new() { ["DeskName"] = "Poste test" });
            var page = WebUtility.HtmlDecode(await opened.Content.ReadAsStringAsync());

            Assert.Contains("Poste test · lettres", page);
            Assert.Contains("Allez chercher les planches", page);

            var closedResponse = await PostDeskAsync(client, "Close");
            closed = true;

            Assert.Contains(
                "Aucun poste de dépôt ouvert sur cet ordinateur.",
                WebUtility.HtmlDecode(await closedResponse.Content.ReadAsStringAsync()));
        }
        finally
        {
            if (!closed)
            {
                await PostDeskAsync(client, "Close");
            }
        }
    }

    [Fact]
    public async Task AComputerHoldingAnOpenDeskCannotOpenASecondOne()
    {
        var client = CreateClient();

        try
        {
            await PostDeskAsync(client, "Open", new() { ["DeskName"] = "Premier poste" });
            var second = await PostDeskAsync(client, "Open", new() { ["DeskName"] = "Second poste" });
            var page = WebUtility.HtmlDecode(await second.Content.ReadAsStringAsync());

            Assert.Contains("Un poste est déjà ouvert sur cet ordinateur.", page);
            Assert.DoesNotContain("Second poste", page);
        }
        finally
        {
            await PostDeskAsync(client, "Close");
        }
    }

    private async Task<long> SeedPreregisteredSellerAsync(int itemCount)
    {
        using var scope = factory.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Services.IntakeService>();
        var seller = await intake.CreateSellerAsync(new AssociationSaleTool.Web.Services.Models.CreateSellerRequest(
            "Alex", $"Depot{Guid.NewGuid():N}"[..12], null, "0601020304", "1 rue des Tests", null));
        await intake.CreateItemBatchAsync(new AssociationSaleTool.Web.Services.Models.CreateItemBatchRequest(
            seller.Id,
            AssociationSaleTool.Web.Services.Models.IntakeMode.EmailPreregistration,
            Enumerable.Range(1, itemCount).Select(i => new AssociationSaleTool.Web.Services.Models.CreateItemRowRequest($"Jouet {i}", 300)).ToList()));
        return seller.Id;
    }

    private async Task<HttpClient> ClientWithOpenDeskAsync(string deskName = "")
    {
        var client = CreateClient();
        await PostDeskAsync(client, "Open", new() { ["DeskName"] = deskName });
        return client;
    }

    // Starts the seller's deposit at a desk opened and closed through the
    // services, so the seller is started while no client holds that desk.
    private async Task StartDepositWithoutClientDeskAsync(long sellerId)
    {
        using var scope = factory.Services.CreateScope();
        var desks = scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Services.DepositDeskService>();
        var desk = await desks.OpenDeskAsync(null);
        try
        {
            var start = await scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Services.IntakeService>()
                .StartDepositAsync(sellerId, desk.DeskId);
            Assert.Equal(AssociationSaleTool.Web.Services.Models.DepositAllocationStatus.Done, start.Status);
        }
        finally
        {
            await desks.CloseDeskAsync(desk.DeskId);
        }
    }

    private static async Task<HttpResponseMessage> PostItemRowAsync(HttpClient client, long sellerId, string title, string price)
    {
        var token = await AntiforgeryTokenAsync(client, $"/vendeurs/{sellerId}");
        return await client.PostAsync($"/vendeurs/{sellerId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.IntakeMode"] = "EmailPreregistration",
            ["Input.Rows[0].Title"] = title,
            ["Input.Rows[0].SellerPrice"] = price
        }));
    }

    [Fact]
    public async Task StartingADepositShowsTheAllocatedReferences()
    {
        var client = await ClientWithOpenDeskAsync();
        try
        {
            var sellerId = await SeedPreregisteredSellerAsync(3);
            Assert.Contains("Commencer le dépôt</button>", WebUtility.HtmlDecode(await client.GetStringAsync($"/vendeurs/{sellerId}")));
            var token = await AntiforgeryTokenAsync(client, $"/vendeurs/{sellerId}");

            var response = await client.PostAsync($"/vendeurs/{sellerId}?handler=StartDeposit", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

            Assert.Matches(@"Références attribuées : [A-Z]\d{3} à [A-Z]\d{3}\.", content);
            Assert.DoesNotContain("Commencer le dépôt</button>", content);
        }
        finally
        {
            await PostDeskAsync(client, "Close");
        }
    }

    [Fact]
    public async Task StartingADepositWithoutADeskIsRefused()
    {
        var client = CreateClient();
        var sellerId = await SeedPreregisteredSellerAsync(1);
        var token = await AntiforgeryTokenAsync(client, $"/vendeurs/{sellerId}");

        var response = await client.PostAsync($"/vendeurs/{sellerId}?handler=StartDeposit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Ouvrez un poste de dépôt sur cet ordinateur pour attribuer des références.", content);
        Assert.Contains("Commencer le dépôt</button>", content);
    }

    [Fact]
    public async Task FinalizingANotStartedDepositIsRefused()
    {
        var client = await ClientWithOpenDeskAsync();
        try
        {
            var sellerId = await SeedPreregisteredSellerAsync(1);
            var token = await AntiforgeryTokenAsync(client, $"/vendeurs/{sellerId}");

            var response = await client.PostAsync($"/vendeurs/{sellerId}?handler=BeginDepositFinalization", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

            Assert.Contains(
                "Commencez le dépôt pour attribuer les références avant de le finaliser.",
                WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }
        finally
        {
            await PostDeskAsync(client, "Close");
        }
    }

    [Fact]
    public async Task SavingRowsOnAStartedDepositShowsTheirReference()
    {
        var sellerId = await SeedPreregisteredSellerAsync(1);
        await StartDepositWithoutClientDeskAsync(sellerId);
        var client = await ClientWithOpenDeskAsync();
        try
        {
            var response = await PostItemRowAsync(client, sellerId, "Toupie ajoutée", "4");
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

            Assert.Matches(@"Référence attribuée : [A-Z]\d{3}\.", content);
            Assert.Contains("Toupie ajoutée", content);
        }
        finally
        {
            await PostDeskAsync(client, "Close");
        }
    }

    [Fact]
    public async Task SavingRowsOnAStartedDepositWithoutADeskIsRefusedAndKeepsTheTypedRows()
    {
        var sellerId = await SeedPreregisteredSellerAsync(1);
        await StartDepositWithoutClientDeskAsync(sellerId);
        var client = CreateClient();

        var response = await PostItemRowAsync(client, sellerId, "Puzzle refusé", "5");
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Ouvrez un poste de dépôt sur cet ordinateur pour attribuer des références.", content);
        Assert.Contains("value=\"Puzzle refusé\"", content);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Data.AppDbContext>();
        Assert.Equal(1, db.Items.Count(item => item.SellerId == sellerId));
    }

    [Fact]
    public async Task SavingRowsAtAClosedDeskIsRefusedAndKeepsTheTypedRows()
    {
        var sellerId = await SeedPreregisteredSellerAsync(1);
        await StartDepositWithoutClientDeskAsync(sellerId);
        var client = await ClientWithOpenDeskAsync("PosteFermeAilleurs");
        using (var scope = factory.Services.CreateScope())
        {
            // An administrator closes this desk from another computer: the cookie stays.
            var db = scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Data.AppDbContext>();
            var deskId = db.DepositDesks.Single(desk => desk.Name == "PosteFermeAilleurs").Id;
            await scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Services.DepositDeskService>().CloseDeskAsync(deskId);
        }

        var response = await PostItemRowAsync(client, sellerId, "Train refusé", "6");
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Ce poste a été fermé. Ouvrez un nouveau poste pour continuer.", content);
        Assert.Contains("value=\"Train refusé\"", content);
    }
}
