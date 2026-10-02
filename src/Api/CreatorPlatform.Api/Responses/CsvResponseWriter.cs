using System.Text;
using CreatorPlatform.Shared.Application.Csv;
using Microsoft.Net.Http.Headers;

namespace CreatorPlatform.Api.Responses;

/// <summary>Streams a CSV download: headers first, then rows as they are produced, flushed periodically — the
/// whole file is never buffered in memory.</summary>
public static class CsvResponseWriter
{
    private const int FlushEveryRows = 500;

    // UTF-8 with a BOM: without it Excel opens UTF-8 CSVs as ANSI and garbles non-ASCII text (č, ć, š, đ, ž).
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static async Task WriteAsync(
        HttpResponse response,
        string fileName,
        string headerRow,
        IAsyncEnumerable<string> rows,
        CancellationToken ct)
    {
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers[HeaderNames.ContentDisposition] =
            new ContentDispositionHeaderValue("attachment") { FileName = fileName }.ToString();
        response.Headers[HeaderNames.CacheControl] = "no-store";

        await using var writer = new StreamWriter(response.Body, Utf8WithBom, bufferSize: 16 * 1024, leaveOpen: true);

        await writer.WriteAsync(headerRow + CsvFormatter.LineEnding);

        var written = 0;
        await foreach (var row in rows.WithCancellation(ct))
        {
            await writer.WriteAsync(row + CsvFormatter.LineEnding);

            if (++written % FlushEveryRows == 0)
                await writer.FlushAsync(ct);
        }

        await writer.FlushAsync(ct);
    }
}
