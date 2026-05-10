namespace AssociationSaleTool.Web.Domain.Sellers;

public static class SellerFeeCalculator
{
    public const int ItemsPerFeeBlock = 15;
    public const int FeePerBlockCents = 200;

    public static int CalculateExpectedFeeCents(int itemCount)
    {
        if (itemCount <= 0)
        {
            return 0;
        }

        var blockCount = (itemCount + ItemsPerFeeBlock - 1) / ItemsPerFeeBlock;
        return blockCount * FeePerBlockCents;
    }

    public static int CalculateRemainingFeeCents(int expectedFeeCents, int paidFeeCents)
    {
        return Math.Max(expectedFeeCents - paidFeeCents, 0);
    }
}
