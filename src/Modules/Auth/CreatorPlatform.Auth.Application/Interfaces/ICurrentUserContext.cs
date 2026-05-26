using CreatorPlatform.Auth.Application.Dtos;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface ICurrentUserContext
{
    CurrentUserDto? User { get; set; }
}
