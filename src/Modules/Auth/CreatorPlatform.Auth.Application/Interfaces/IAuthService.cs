using CreatorPlatform.Auth.Application.Dtos;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IAuthService
{
    Task<RegisterUserResponseDto> RegisterAsync(RegisterUserRequestDto request, CancellationToken ct);
    Task<LoginUserResultDto> LoginAsync(LoginUserRequestDto request, CancellationToken ct);
    Task<VerifyEmailResponseDto> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken ct);
    Task<ResendEmailVerificationResponseDto> ResendEmailVerificationAsync(
        ResendEmailVerificationRequestDto request,
        CancellationToken ct);
}
