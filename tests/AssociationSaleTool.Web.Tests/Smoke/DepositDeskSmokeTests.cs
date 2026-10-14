using System.Net;
using System.Text.RegularExpressions;
using AssociationSaleTool.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

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
}
