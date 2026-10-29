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

    private static async Task<HttpResponseMessage> PostDeskAsync(HttpClient client, long sellerId, string handler)
    {
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await AntiforgeryTokenAsync(client, $"/vendeurs/{sellerId}")
        };
        return await client.PostAsync($"/vendeurs/{sellerId}?handler={handler}", new FormUrlEncodedContent(form));
    }

    private static async Task<string> PageAfterAsync(HttpResponseMessage response)
    {
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithoutADeskTheSellerPageDisablesTheDepositActionsAndOffersToOpenOne()
    {
        var client = CreateClient();
        var sellerId = await SeedPreregisteredSellerAsync(1);

        var content = WebUtility.HtmlDecode(await client.GetStringAsync($"/vendeurs/{sellerId}"));

        Assert.Contains("Prenez des lettres pour étiqueter les jouets ou finaliser le dépôt.", content);
        Assert.Contains("Prendre des lettres</button>", content);
        Assert.Matches(@"disabled=""disabled"">Étiqueter les jouets</button>", content);
        Assert.Matches(@"data-seller-deposit-begin disabled=""disabled"">", content);
        Assert.DoesNotContain("data-deposit-desk-banner", content);
    }

    [Fact]
    public async Task OpeningADeskShowsItsLettersOnTheSellerPageAndReleasingThemRemovesTheCookie()
    {
        var client = CreateClient();
        var sellerId = await SeedPreregisteredSellerAsync(1);
        var released = false;

        try
        {
            var page = await PageAfterAsync(await PostDeskAsync(client, sellerId, "OpenDesk"));

            Assert.Matches(@"Allez chercher les planches [A-Z] et [A-Z]\.", page);
            Assert.Matches(@"Lettres de cet ordinateur :\s+<strong>[A-Z], [A-Z]</strong>", page);
            Assert.DoesNotContain("Poste ", page);
            Assert.Contains("Libérer les lettres</button>", page);
            Assert.DoesNotContain("Prendre des lettres</button>", page);
            Assert.Contains("Étiqueter les jouets</button>", page);
            Assert.DoesNotMatch(@"disabled=""disabled"">Étiqueter les jouets</button>", page);

            var afterRelease = await PageAfterAsync(await PostDeskAsync(client, sellerId, "ReleaseDesk"));
            released = true;

            Assert.Matches(@"Lettres [A-Z] et [A-Z] libérées\. Rapportez leurs planches sur la table centrale\.", afterRelease);
            Assert.Contains("Prenez des lettres pour étiqueter les jouets ou finaliser le dépôt.", afterRelease);
        }
        finally
        {
            if (!released)
            {
                await PostDeskAsync(client, sellerId, "ReleaseDesk");
            }
        }
    }

    [Fact]
    public async Task AComputerHoldingAnOpenDeskDoesNotOpenASecondOne()
    {
        var client = CreateClient();
        var sellerId = await SeedPreregisteredSellerAsync(1);

        try
        {
            await PostDeskAsync(client, sellerId, "OpenDesk");
            var openBefore = OpenDeskCount();
            await PostDeskAsync(client, sellerId, "OpenDesk");

            Assert.Equal(openBefore, OpenDeskCount());
        }
        finally
        {
            await PostDeskAsync(client, sellerId, "ReleaseDesk");
        }
    }

    private int OpenDeskCount()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Data.AppDbContext>();
        return db.DepositDesks.Count(desk => desk.ClosedAt == null);
    }

    // The desk this test just opened: the newest one, tests of a class run one at a time.
    private long NewestDeskId()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Data.AppDbContext>();
        return db.DepositDesks.OrderByDescending(desk => desk.Id).First().Id;
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

    private async Task<HttpClient> ClientWithOpenDeskAsync(long sellerId)
    {
        var client = CreateClient();
        await PostDeskAsync(client, sellerId, "OpenDesk");
        return client;
    }

    // Starts the seller's deposit at a desk opened and closed through the
    // services, so the seller is started while no client holds that desk.
    private async Task StartDepositWithoutClientDeskAsync(long sellerId)
    {
        using var scope = factory.Services.CreateScope();
        var desks = scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Services.DepositDeskService>();
        var desk = await desks.OpenDeskAsync();
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
        var sellerId = await SeedPreregisteredSellerAsync(3);
        var client = await ClientWithOpenDeskAsync(sellerId);
        try
        {
            Assert.Contains("Étiqueter les jouets</button>", WebUtility.HtmlDecode(await client.GetStringAsync($"/vendeurs/{sellerId}")));
            var token = await AntiforgeryTokenAsync(client, $"/vendeurs/{sellerId}");

            var response = await client.PostAsync($"/vendeurs/{sellerId}?handler=StartDeposit", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

            Assert.Matches(@"3 jouets étiquetés : [A-Z]\d{3} à [A-Z]\d{3}\.", content);
            Assert.Contains("collez-les sur les jouets surlignés ci-dessous.", content);
            Assert.Equal(3, Regex.Matches(content, "class=\"seller-item-just-labelled\"").Count);
            // The message sits in the articles section the redirect scrolls to.
            Assert.True(content.IndexOf("deposit-allocation-message", StringComparison.Ordinal)
                > content.IndexOf("id=\"articles\"", StringComparison.Ordinal));
            Assert.DoesNotContain("Étiqueter les jouets</button>", content);
        }
        finally
        {
            await PostDeskAsync(client, sellerId, "ReleaseDesk");
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

        Assert.Contains("Prenez des lettres pour attribuer des références.", content);
        Assert.Contains("Étiqueter les jouets</button>", content);
    }

    [Fact]
    public async Task FinalizingANotStartedDepositIsRefused()
    {
        var sellerId = await SeedPreregisteredSellerAsync(1);
        var client = await ClientWithOpenDeskAsync(sellerId);
        try
        {
            var token = await AntiforgeryTokenAsync(client, $"/vendeurs/{sellerId}");

            var response = await client.PostAsync($"/vendeurs/{sellerId}?handler=BeginDepositFinalization", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

            Assert.Contains(
                "Étiquetez les jouets avant de finaliser le dépôt.",
                WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }
        finally
        {
            await PostDeskAsync(client, sellerId, "ReleaseDesk");
        }
    }

    [Fact]
    public async Task SavingRowsOnAStartedDepositShowsTheirReference()
    {
        var sellerId = await SeedPreregisteredSellerAsync(1);
        await StartDepositWithoutClientDeskAsync(sellerId);
        var client = await ClientWithOpenDeskAsync(sellerId);
        try
        {
            var response = await PostItemRowAsync(client, sellerId, "Toupie ajoutée", "4");
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

            Assert.Matches(@"1 jouet étiqueté : [A-Z]\d{3}\.", content);
            Assert.Equal(1, Regex.Matches(content, "class=\"seller-item-just-labelled\"").Count);
            Assert.Contains("Toupie ajoutée", content);
        }
        finally
        {
            await PostDeskAsync(client, sellerId, "ReleaseDesk");
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

        Assert.Contains("Prenez des lettres pour attribuer des références.", content);
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
        var client = await ClientWithOpenDeskAsync(sellerId);
        var deskId = NewestDeskId();
        using (var scope = factory.Services.CreateScope())
        {
            // An administrator closes this desk from another computer: the cookie stays.
            await scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Services.DepositDeskService>().CloseDeskAsync(deskId);
        }

        var response = await PostItemRowAsync(client, sellerId, "Train refusé", "6");
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Les lettres de cet ordinateur ont été libérées. Reprenez des lettres pour continuer.", content);
        Assert.Contains("value=\"Train refusé\"", content);
    }

    private async Task UnlockAdminAsync(HttpClient client)
    {
        var token = await AntiforgeryTokenAsync(client, "/administration");
        await client.PostAsync("/administration?handler=Unlock", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["PasswordInput.Password"] = "test-password"
        }));
    }

    [Fact]
    public async Task AdminForceCloseMakesTheDeskComputerSeeItsDeskClosed()
    {
        var sellerId = await SeedPreregisteredSellerAsync(1);
        var deskClient = await ClientWithOpenDeskAsync(sellerId);
        var deskId = NewestDeskId();
        var admin = CreateClient();
        await UnlockAdminAsync(admin);
        var adminPage = await admin.GetStringAsync("/administration?onglet=lettres");
        Assert.Contains($"name=\"DeskId\" value=\"{deskId}\"", adminPage);
        var token = await AntiforgeryTokenAsync(admin, "/administration?onglet=lettres");

        await admin.PostAsync("/administration?handler=ForceCloseDesk", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DeskId"] = deskId.ToString()
        }));

        var sellerPage = WebUtility.HtmlDecode(await deskClient.GetStringAsync($"/vendeurs/{sellerId}"));
        Assert.Contains("Les lettres de cet ordinateur ont été libérées. Reprenez des lettres pour continuer le dépôt.", sellerPage);
        Assert.Contains("Prendre des lettres</button>", sellerPage);
    }

    [Fact]
    public async Task AdminCannotSetLabelsPerLetterAboveTheFormatLimit()
    {
        var admin = CreateClient();
        await UnlockAdminAsync(admin);
        var token = await AntiforgeryTokenAsync(admin, "/administration");

        var response = await admin.PostAsync("/administration?handler=SaveLabelsPerLetter", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["LabelsPerLetterInput"] = "1000"
        }));

        Assert.Contains(
            "Le nombre d'étiquettes par lettre doit être compris entre 1 et 999.",
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }
}
