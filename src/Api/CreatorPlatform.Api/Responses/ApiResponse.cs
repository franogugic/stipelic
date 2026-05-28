namespace CreatorPlatform.Api.Responses;

public sealed class ApiResponse<T>
{
    public int StatusCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public T? Data { get; init; }

    public static ApiResponse<T> Success(int statusCode, string message, T? data)
    {
        return new ApiResponse<T>
        {
            StatusCode = statusCode,
            Message = message,
            Code = "SUCCESS",
            Data = data
        };
    }
}
