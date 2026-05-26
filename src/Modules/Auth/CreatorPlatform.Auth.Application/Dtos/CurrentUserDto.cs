namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record CurrentUserDto
{
    public int Id { get; init; }
    public Guid SessionId { get; init; }
    public Guid PublicId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsEmailVerified { get; init; }
    public string Status { get; init; } = string.Empty;
}
