using CreatorPlatform.Shared.Application.Csv;

namespace CreatorPlatform.MoneyPath.Tests.Csv;

public class CsvFormatterTests
{
    [Theory]
    [InlineData("plain@example.com", "plain@example.com")]
    [InlineData("Landing; Page", "Landing; Page")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("line1\r\nline2", "\"line1\r\nline2\"")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Field_EscapesPerRfc4180(string? value, string expected)
    {
        Assert.Equal(expected, CsvFormatter.Field(value));
    }

    [Theory]
    [InlineData("=SUM(A1:A9)", "'=SUM(A1:A9)")]
    [InlineData("+3612345", "'+3612345")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\tTabbed", "'\tTabbed")]
    public void Field_PrefixesFormulaTriggersWithAQuote(string value, string expected)
    {
        Assert.Equal(expected, CsvFormatter.Field(value));
    }

    [Fact]
    public void Field_LeadingCarriageReturn_IsGuardedAndThenQuoted()
    {
        Assert.Equal("\"'\rx\"", CsvFormatter.Field("\rx"));
    }

    [Fact]
    public void Field_FormulaWithACommaAndQuotes_IsGuardedThenQuoted()
    {
        Assert.Equal("\"'=HYPERLINK(\"\"http://x\"\",\"\"y\"\")\"", CsvFormatter.Field("=HYPERLINK(\"http://x\",\"y\")"));
    }

    [Theory]
    [InlineData("a=b")]
    [InlineData("name+tag@example.com")]
    [InlineData("x-y")]
    public void Field_TriggerCharacterNotAtTheStart_IsLeftAlone(string value)
    {
        Assert.Equal(value, CsvFormatter.Field(value));
    }

    [Fact]
    public void Row_JoinsEscapedFieldsWithCommas()
    {
        Assert.Equal("a,\"b,c\",'=1,", CsvFormatter.Row("a", "b,c", "=1", null));
    }

    [Fact]
    public void Timestamp_IsIso8601UtcWithSeconds()
    {
        var local = new DateTimeOffset(2026, 10, 2, 11, 15, 30, TimeSpan.FromHours(2));

        Assert.Equal("2026-10-02T09:15:30Z", CsvFormatter.Timestamp(local));
    }
}

public class CsvMoneyFormattingTests
{
    [Theory]
    [InlineData(2900L, "29.00")]
    [InlineData(2999L, "29.99")]
    [InlineData(5L, "0.05")]
    [InlineData(50L, "0.50")]
    [InlineData(0L, "0.00")]
    [InlineData(100000000L, "1000000.00")]
    [InlineData(-150L, "-1.50")]
    [InlineData(-5L, "-0.05")]
    [InlineData(long.MaxValue, "92233720368547758.07")]
    public void Money_FormatsCentsWithTwoDecimalsAndADot(long cents, string expected)
    {
        Assert.Equal(expected, CsvFormatter.Money(cents));
    }

    [Fact]
    public void Money_IgnoresTheCurrentCulture()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("hr-HR"); // decimal comma
            Assert.Equal("1234.56", CsvFormatter.Money(123456));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
