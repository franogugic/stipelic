using System.Text.Json.Serialization;

namespace CreatorPlatform.Api.Responses;

public sealed class ApiErrorResponse
{
    public int StatusCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    /// <summary>Extra data for some errors (see <c>ConflictException.Details</c>); omitted when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; init; }
}
