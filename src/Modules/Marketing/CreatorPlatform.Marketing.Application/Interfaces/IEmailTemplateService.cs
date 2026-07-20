using CreatorPlatform.Marketing.Application.Dtos;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IEmailTemplateService
{
    /// <summary>Active templates first, then Archived — both alphabetical by name within each group.</summary>
    Task<List<EmailTemplateDto>> ListAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<EmailTemplateDto> GetAsync(string slug, int ownerUserId, Guid templatePublicId, CancellationToken ct);

    Task<EmailTemplateDto> CreateAsync(string slug, int ownerUserId, SaveEmailTemplateRequestDto request, CancellationToken ct);

    /// <summary>Active-only — throws <see cref="Shared.Application.Exceptions.ConflictException"/> once archived.</summary>
    Task<EmailTemplateDto> UpdateAsync(string slug, int ownerUserId, Guid templatePublicId, SaveEmailTemplateRequestDto request, CancellationToken ct);

    Task<EmailTemplateDto> ArchiveAsync(string slug, int ownerUserId, Guid templatePublicId, CancellationToken ct);
}
