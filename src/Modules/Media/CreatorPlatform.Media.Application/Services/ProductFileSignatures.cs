
namespace CreatorPlatform.Media.Application.Services;

/// <summary>
/// Checks that an uploaded product document really is what its content type says, from its first bytes: a PDF starts
/// with <c>%PDF-</c>; a ZIP with the local-file-header signature <c>PK\x03\x04</c>; an EPUB is a ZIP whose first entry is
/// an uncompressed <c>mimetype</c> file containing <c>application/epub+zip</c> (EPUB OCF).
/// </summary>
public static class ProductFileSignatures
{
    public const string Pdf = "application/pdf";
    public const string Zip = "application/zip";
    public const string Epub = "application/epub+zip";

    /// <summary>Enough for the EPUB check (30-byte header + "mimetype" + its content).</summary>
    public const int HeadLength = 128;

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] EpubEntryName = "mimetype"u8.ToArray();
    private static readonly byte[] EpubMimeType = "application/epub+zip"u8.ToArray();

    public static bool Matches(string contentType, ReadOnlySpan<byte> head) => contentType.ToLowerInvariant() switch
    {
        Pdf => head.StartsWith(PdfMagic),
        Zip => head.StartsWith(ZipMagic),
        Epub => IsEpub(head),
        _ => false
    };

    public static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        Pdf => "pdf",
        Epub => "epub",
        Zip => "zip",
        _ => "bin"
    };

    private static bool IsEpub(ReadOnlySpan<byte> head)
    {
        // ZIP local file header: signature (4) … compression method at 8 (2), name length at 26 (2), extra length at
        // 28 (2), name at 30, then the extra field, then the data.
        if (head.Length < 30 || !head.StartsWith(ZipMagic))
            return false;

        var compression = head[8] | head[9] << 8;
        var nameLength = head[26] | head[27] << 8;
        var extraLength = head[28] | head[29] << 8;
        var dataStart = 30 + nameLength + extraLength;

        return compression == 0
            && nameLength == EpubEntryName.Length
            && head.Length >= dataStart + EpubMimeType.Length
            && head.Slice(30, nameLength).SequenceEqual(EpubEntryName)
            && head.Slice(dataStart, EpubMimeType.Length).SequenceEqual(EpubMimeType);
    }
}
