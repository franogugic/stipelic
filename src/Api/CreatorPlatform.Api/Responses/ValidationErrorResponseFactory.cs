using Microsoft.AspNetCore.Mvc;

namespace CreatorPlatform.Api.Responses;

public static class ValidationErrorResponseFactory
{
    public static BadRequestObjectResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => ToCamelCase(entry.Key),
                entry => entry.Value!.Errors
                    .Select(error => error.ErrorMessage)
                    .ToArray());

        var response = new ValidationErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = "Validation failed.",
            Code = "VALIDATION_FAILED",
            Errors = errors
        };

        return new BadRequestObjectResult(response);
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
