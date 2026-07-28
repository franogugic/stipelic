using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Application.Options;
using CreatorPlatform.Auth.Domain.Users;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Api.Middlewares;

public sealed class SessionAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOptions<AuthOptions> authOptions,
        ITokenHasher tokenHasher,
        IUserSessionRepository userSessionRepository,
        IUserRepository userRepository,
        ICurrentUserContext currentUserContext)
    {
        var cookieName = authOptions.Value.SessionCookieName;
        var sessionToken = context.Request.Cookies[cookieName];
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            var sessionTokenHash = tokenHasher.Hash(sessionToken);
            var now = DateTimeOffset.UtcNow;
            var session = await userSessionRepository.GetValidByTokenHashAsync(sessionTokenHash, now, context.RequestAborted);

            if (session is not null)
            {
                var result = await userRepository.GetByIdWithRolesAsync(session.UserId, context.RequestAborted);

                if (result is not null && result.User.Status is not UserStatus.Disabled)
                {
                    var user = result.User;
                    currentUserContext.User = new CurrentUserDto
                    {
                        Id = user.Id,
                        SessionId = session.Id,
                        PublicId = user.PublicId,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        IsEmailVerified = user.IsEmailVerified,
                        Status = user.Status.ToString(),
                        Roles = result.Roles
                    };
                }
            }
        }

        await _next(context);
    }
}
