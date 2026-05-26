using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Interfaces;

namespace CreatorPlatform.Auth.Infrastructure.Security;

public sealed class CurrentUserContext : ICurrentUserContext
{
    public CurrentUserDto? User { get; set; }
}
