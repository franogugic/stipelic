namespace CreatorPlatform.Access.Application.Interfaces;

public interface IAccessRedirectService
{
    Task<string?> GetAccessUrlAsync(Guid orderPublicId, CancellationToken ct);
}
