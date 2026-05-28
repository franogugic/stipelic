namespace CreatorPlatform.Api.Responses;

public sealed class ApiErrorResponse
{
    public int StatusCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;
}
