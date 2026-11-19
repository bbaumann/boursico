using AssociationSaleTool.Web.Domain.Search;

namespace AssociationSaleTool.Web.Tests.Domain;

public sealed class TextSearchTests
{
    [Theory]
    [InlineData("pompier camion", "Camion de pompiers")]
    [InlineData("camions de pompiers", "Camion pompier")]
    [InlineData("elephant peluche", "Peluche Éléphant")]
    [InlineData("ECOLE", "école en bois")]
    [InlineData("coeur", "Cœur en tissu")]
    [InlineData("jeux societe", "Jeu de société")]
    [InlineData("chevaux", "Cheval à bascule")]
    [InlineData("pomp", "Camion de pompiers")]
    [InlineData("b-157", "B157 Puzzle")]
    [InlineData("le", "Lego Duplo")]
    public void Matches_FindsTextRegardlessOfWordOrderAccentsAndPlurals(string query, string text)
    {
        Assert.True(TextSearch.Parse(query).Matches(text));
    }

    [Theory]
    [InlineData("camion rouge", "Camion de pompiers")]
    [InlineData("poupee", "Camion de pompiers")]
    public void Matches_RequiresEveryMeaningfulWord(string query, string text)
    {
        Assert.False(TextSearch.Parse(query).Matches(text));
    }

    [Fact]
    public void Matches_LooksAcrossAllFields()
    {
        Assert.True(TextSearch.Parse("hopper puzzle").Matches("B001", "Puzzle", "Grace Hopper"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  - ' ")]
    public void Parse_TreatsBlankOrPunctuationOnlyQueriesAsEmpty(string? query)
    {
        Assert.True(TextSearch.Parse(query).IsEmpty);
    }
}
