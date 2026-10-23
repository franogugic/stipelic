namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>One cell of the email column. <see cref="Row"/> is the row number as a spreadsheet shows it.</summary>
public sealed record ContactCsvCell(int Row, string Value);

/// <summary>Reads the email column of a contacts CSV: UTF-8 with or without BOM, <c>,</c> or <c>;</c> (detected), RFC
/// 4180 quoting; the column headed <c>email</c> / <c>e-mail</c> (any case), or the only column, whose header is
/// optional. Blank lines are skipped. A file it cannot use is a <c>BadRequestException</c> with a
/// <see cref="ContactImportErrors"/> code; more than <paramref name="maxRows"/> data rows is one too.</summary>
public interface IContactCsvReader
{
    Task<List<ContactCsvCell>> ReadEmailColumnAsync(Stream file, int maxRows, CancellationToken ct);
}
