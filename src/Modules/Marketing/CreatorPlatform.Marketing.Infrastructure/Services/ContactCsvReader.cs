using System.Globalization;
using System.Text;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using CsvHelper;
using CsvHelper.Configuration;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

/// <summary>
/// The email column of a contacts CSV, read with CsvHelper (RFC 4180 quoting, <c>,</c> / <c>;</c> detected). The file
/// is decoded as strict UTF-8 (a BOM is accepted and dropped); any other encoding is refused rather than guessed, so an
/// address can never be imported mangled. With several columns the header must name the email column
/// (<c>email</c> / <c>e-mail</c>, any case); with one column the header is optional. Blank lines are skipped.
/// </summary>
public sealed class ContactCsvReader : IContactCsvReader
{
    private static readonly string[] EmailHeaders = ["email", "e-mail"];

    public async Task<List<ContactCsvCell>> ReadEmailColumnAsync(Stream file, int maxRows, CancellationToken ct)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        using var text = new StreamReader(file, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            DetectDelimiter = true,
            DetectDelimiterValues = [",", ";"],
            IgnoreBlankLines = true,
            MissingFieldFound = null
        };
        using var csv = new CsvReader(text, configuration);

        try
        {
            if (!await csv.ReadAsync())
                return [];

            var first = csv.Parser.Record ?? [];
            int column;
            bool firstIsHeader;
            if (first.Length > 1)
            {
                column = Array.FindIndex(first, IsEmailHeader);
                if (column < 0)
                    throw new BadRequestException(
                        "The file has several columns, and none is headed \"email\".", ContactImportErrors.NoEmailColumn);
                firstIsHeader = true;
            }
            else
            {
                column = 0;
                firstIsHeader = first.Length == 1 && IsEmailHeader(first[0]);
            }

            var cells = new List<ContactCsvCell>();
            if (!firstIsHeader)
                cells.Add(new ContactCsvCell(csv.Parser.Row, Cell(first, column)));

            while (await csv.ReadAsync())
            {
                ct.ThrowIfCancellationRequested();
                cells.Add(new ContactCsvCell(csv.Parser.Row, Cell(csv.Parser.Record ?? [], column)));
                if (cells.Count > maxRows)
                    throw new BadRequestException(
                        $"The file has more than {maxRows:N0} rows. Split it into smaller files.", ContactImportErrors.TooManyRows);
            }

            return cells;
        }
        catch (DecoderFallbackException)
        {
            throw new BadRequestException("The file is not UTF-8 text. Save it as \"CSV UTF-8\" and try again.", ContactImportErrors.InvalidEncoding);
        }
        catch (BadDataException exception)
        {
            throw new BadRequestException(
                $"The file could not be read as CSV (row {exception.Context?.Parser?.Row}): check its quotes.", ContactImportErrors.InvalidCsv);
        }
    }

    private static bool IsEmailHeader(string? value) =>
        EmailHeaders.Contains(value?.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string Cell(string[] record, int column) => column < record.Length ? record[column] : string.Empty;
}
