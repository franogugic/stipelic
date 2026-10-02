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
