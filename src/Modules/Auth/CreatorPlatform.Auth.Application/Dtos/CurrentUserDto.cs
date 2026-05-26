namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record CurrentUserDto
{
    public int Id { get; init; }
    public Guid PublicId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}
