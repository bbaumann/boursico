using AssociationSaleTool.Web.Domain.Sellers;

namespace AssociationSaleTool.Web.Tests.Domain;

public class SellerCodeGeneratorTests
{
    [Fact]
    public void GeneratesSellerCodesInSpreadOrder()
    {
        Assert.Equal("BB", SellerCodeGenerator.GetCodeAt(0));
        Assert.Equal("CB", SellerCodeGenerator.GetCodeAt(1));
        Assert.Equal("ZB", SellerCodeGenerator.GetCodeAt(19));
        Assert.Equal("BC", SellerCodeGenerator.GetCodeAt(20));
    }
}
