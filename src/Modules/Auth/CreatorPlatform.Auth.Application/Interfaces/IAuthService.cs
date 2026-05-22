using CreatorPlatform.Auth.Application.Dtos;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IAuthService
{
    Task<RegisterUserResponseDto> RegisterAsync(RegisterUserRequestDto request, CancellationToken ct);
    Task<VerifyEmailResponseDto> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken ct);
}
