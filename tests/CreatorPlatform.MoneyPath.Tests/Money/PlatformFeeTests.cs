using CreatorPlatform.Shared.Domain.Money;

namespace CreatorPlatform.MoneyPath.Tests.Money;

public class PlatformFeeTests
{
    [Fact]
    public void Calculate_ZeroBasisPoints_ReturnsZero()
    {
        Assert.Equal(0, PlatformFee.Calculate(100_00, 0));
    }

    [Fact]
    public void Calculate_FullBasisPoints_ReturnsFullAmount()
    {
        Assert.Equal(100_00, PlatformFee.Calculate(100_00, 10_000));
    }

    [Fact]
    public void Calculate_RoundsDown_InFavorOfCreator()
    {
        // 999 cents at 5% (500 bps) = 49.95 -> floors to 49, never 50.
        Assert.Equal(49, PlatformFee.Calculate(999, 500));
    }

    [Fact]
    public void Calculate_SmallestFraction_RoundsDownToZero()
    {
        Assert.Equal(0, PlatformFee.Calculate(1, 1));
    }

    [Fact]
    public void Calculate_LargeAmount_NoOverflow()
    {
        Assert.Equal(int.MaxValue, PlatformFee.Calculate(int.MaxValue, 10_000));
    }

    [Theory]
    [InlineData(-1, 500)]
    [InlineData(100, -1)]
    [InlineData(100, 10_001)]
    public void Calculate_OutOfRangeInputs_Throws(int amountCents, int feeBasisPoints)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformFee.Calculate(amountCents, feeBasisPoints));
    }
}
