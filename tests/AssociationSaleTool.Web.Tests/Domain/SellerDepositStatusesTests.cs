using AssociationSaleTool.Web.Domain.Sellers;

namespace AssociationSaleTool.Web.Tests.Domain;

public class SellerDepositStatusesTests
{
    private static readonly DateTime Started = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void APendingDepositWhoseToysAreNotLabelledIsNotStarted()
    {
        Assert.Equal("Dépôt non commencé", SellerDepositStatuses.ToFrenchLabel(SellerDepositStatuses.Pending, null));
        Assert.Equal(
            "seller-deposit-status-not-started",
            SellerDepositStatuses.ToCssClass(SellerDepositStatuses.Pending, null));
    }

    [Fact]
    public void APendingDepositWhoseToysAreLabelledIsToValidate()
    {
        Assert.Equal("Dépôt à valider", SellerDepositStatuses.ToFrenchLabel(SellerDepositStatuses.Pending, Started));
        Assert.Equal(
            "seller-deposit-status-pending",
            SellerDepositStatuses.ToCssClass(SellerDepositStatuses.Pending, Started));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AValidatedDepositIsValidated(bool started)
    {
        // A validated deposit was always started; a missing date (legacy data) must not show it as not started.
        DateTime? startedAt = started ? Started : null;

        Assert.Equal("Dépôt validé", SellerDepositStatuses.ToFrenchLabel(SellerDepositStatuses.Validated, startedAt));
        Assert.Equal(
            "seller-deposit-status-validated",
            SellerDepositStatuses.ToCssClass(SellerDepositStatuses.Validated, startedAt));
    }
}
