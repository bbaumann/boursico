using AssociationSaleTool.Web.Domain.Sellers;

namespace AssociationSaleTool.Web.Tests.Domain;

public class SellerCodeGeneratorTests
{
    [Fact]
    public void GeneratesSellerCodesInSpreadOrder()
    {
        Assert.Equal("AA", SellerCodeGenerator.GetCodeAt(0));
        Assert.Equal("BA", SellerCodeGenerator.GetCodeAt(1));
        Assert.Equal("ZA", SellerCodeGenerator.GetCodeAt(21));
        Assert.Equal("AB", SellerCodeGenerator.GetCodeAt(22));
    }
}
