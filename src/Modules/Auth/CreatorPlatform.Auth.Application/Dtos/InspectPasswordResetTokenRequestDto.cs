namespace CreatorPlatform.Auth.Application.Dtos;

/// <remarks>No <c>[Required]</c>: a missing or blank token must answer the same 400 message as an unknown one
/// (handled in the service), not the model-validation error shape.</remarks>
public sealed record InspectPasswordResetTokenRequestDto
{
    public string? Token { get; init; }
}
