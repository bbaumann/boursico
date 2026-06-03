namespace AssociationSaleTool.Web.Services;

public sealed class InvalidSellerPostalCityException : InvalidOperationException
{
    public const string DefaultMessage = "La ville doit correspondre au code postal sélectionné.";

    public InvalidSellerPostalCityException()
        : base(DefaultMessage)
    {
    }
}
