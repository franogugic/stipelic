namespace CreatorPlatform.Api.Responses;

public sealed class ValidationErrorResponse
{
    public int StatusCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string[]> Errors { get; init; } =
        new Dictionary<string, string[]>();
}
