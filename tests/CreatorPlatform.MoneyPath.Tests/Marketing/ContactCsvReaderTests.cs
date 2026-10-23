using System.Text;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

/// <summary>Reading the email column of a contacts CSV, and checking each email like a page sign-up.</summary>
public class ContactCsvReaderTests
{
    private static readonly ContactCsvReader Reader = new();

    private static Task<List<ContactCsvCell>> ReadAsync(string text, bool bom = false, int maxRows = 10_000)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bom)
            bytes = [0xEF, 0xBB, 0xBF, .. bytes];
        return Reader.ReadEmailColumnAsync(new MemoryStream(bytes), maxRows, CancellationToken.None);
    }

    private static async Task<string> RefusedAsync(Func<Task> read) =>
        (await Assert.ThrowsAsync<BadRequestException>(read)).Code;

    [Fact]
    public async Task CommaSeparated_FindsTheEmailColumnByItsHeader_WithSpreadsheetRowNumbers()
    {
        var cells = await ReadAsync("Name,E-Mail,Note\nAna,ana@example.com,hi\n\nBen,\"ben@example.com\",\"a, b\"\n");

        Assert.Equal([new ContactCsvCell(2, "ana@example.com"), new ContactCsvCell(4, "ben@example.com")], cells);
    }

    [Fact]
    public async Task SemicolonSeparated_WithABom_IsDetected()
    {
        var cells = await ReadAsync("first;EMAIL\r\nAna;ana@example.com\r\nBen;ben@example.com\r\n", bom: true);

        Assert.Equal(["ana@example.com", "ben@example.com"], cells.Select(c => c.Value));
    }

    [Fact]
    public async Task ASingleColumn_NeedsNoHeader()
    {
        Assert.Equal(
            [new ContactCsvCell(1, "ana@example.com"), new ContactCsvCell(2, "ben@example.com")],
            await ReadAsync("ana@example.com\nben@example.com"));
        Assert.Equal([new ContactCsvCell(2, "ana@example.com")], await ReadAsync("email\nana@example.com\n", bom: true));
    }

    [Fact]
    public async Task AShortRow_GivesAnEmptyCell()
    {
        var cells = await ReadAsync("name,email\nAna\nBen,ben@example.com");

        Assert.Equal([new ContactCsvCell(2, ""), new ContactCsvCell(3, "ben@example.com")], cells);
    }

    [Fact]
    public async Task AnEmptyFile_HasNoCells() => Assert.Empty(await ReadAsync(""));

    [Fact]
    public async Task SeveralColumnsWithoutAnEmailHeader_AreRefused() =>
        Assert.Equal(ContactImportErrors.NoEmailColumn, await RefusedAsync(() => ReadAsync("name,mail\nAna,ana@example.com")));

    [Fact]
    public async Task MoreRowsThanTheLimit_AreRefused()
    {
        Assert.Equal(3, (await ReadAsync("email\na@x.test\nb@x.test\nc@x.test", maxRows: 3)).Count);
        Assert.Equal(ContactImportErrors.TooManyRows,
            await RefusedAsync(() => ReadAsync("email\na@x.test\nb@x.test\nc@x.test\nd@x.test", maxRows: 3)));
    }

    [Fact]
    public async Task NonUtf8Text_IsRefusedRatherThanGuessed()
    {
        var latin1 = new byte[] { (byte)'e', (byte)'m', (byte)'a', (byte)'i', (byte)'l', (byte)'\n', (byte)'j', 0xF6, (byte)'@', (byte)'x', (byte)'.', (byte)'t' };

        var code = await RefusedAsync(() => Reader.ReadEmailColumnAsync(new MemoryStream(latin1), 100, CancellationToken.None));

        Assert.Equal(ContactImportErrors.InvalidEncoding, code);
    }

    [Fact]
    public async Task BrokenQuoting_IsRefused() =>
        Assert.Equal(ContactImportErrors.InvalidCsv, await RefusedAsync(() => ReadAsync("name,email\n\"Ana,ana@example.com\nBen\"x\",b@x.test")));

    [Theory]
    [InlineData("  Ana@Example.COM ", "ana@example.com", null)]
    [InlineData("", null, "EMPTY")]
    [InlineData("   ", null, "EMPTY")]
    [InlineData("not-an-email", null, "INVALID_EMAIL")]
    [InlineData("two@@at.test", null, "INVALID_EMAIL")]
    public void EmailsAreCheckedLikeASignUp(string raw, string? normalized, string? reason)
    {
        var check = ContactEmailRules.Check(raw);

        Assert.Equal((normalized, reason), (check.Normalized, check.Reason));
    }

    [Fact]
    public void AnEmailOver254Characters_IsTooLong_AndItsValueIsCut()
    {
        var check = ContactEmailRules.Check(new string('a', 250) + "@example.com");

        Assert.Equal(("TOO_LONG", 254), (check.Reason, check.Value.Length));
    }
}
