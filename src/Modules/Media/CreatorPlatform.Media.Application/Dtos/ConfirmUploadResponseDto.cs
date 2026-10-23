using System.Text.Json.Serialization;

namespace CreatorPlatform.Media.Application.Dtos;

/// <summary>An image upload answers <c>{ url }</c>, its public URL. A product document has no URL at all: it answers
/// <c>{ fileReference, fileName, fileSizeBytes, contentType }</c> — an opaque reference the product save takes, and what
/// the server found when it read the real blob.</summary>
public sealed record ConfirmUploadResponseDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? FileReference { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? FileSizeBytes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentType { get; init; }
}
