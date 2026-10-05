using CreatorPlatform.Orders.Application.Receipts;

namespace CreatorPlatform.MoneyPath.Tests.Orders;

public class OrderReceiptFormattingTests
{
    [Theory]
    [InlineData("ana.kovacevic@gmail.com", "a•••@gmail.com")]
    [InlineData("a@example.test", "a•••@example.test")]
    [InlineData("  Marko@Firma.hr  ", "M•••@Firma.hr")]
    [InlineData("weird@name@example.test", "w•••@example.test")]
    [InlineData("no-at-sign", "•••")]
    [InlineData("@example.test", "•••")]
    [InlineData("trailing@", "•••")]
    public void EmailMask_KeepsOnlyTheFirstCharacterAndTheDomain(string email, string expected)
    {
        Assert.Equal(expected, EmailMask.Mask(email));
    }

    [Fact]
    public void OrderNumber_IsTheFirstEightHexDigitsOfThePublicId_UpperCase()
    {
        Assert.Equal("A1B2C3D4", OrderNumbers.From(Guid.Parse("a1b2c3d4-0000-4000-8000-000000000000")));
    }
}
