using CreatorPlatform.Marketing.Domain.Templates;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IEmailTemplateRepository
{
    Task AddAsync(EmailTemplate template, CancellationToken ct);

    /// <summary>Tracked — for update/archive mutations.</summary>
    Task<EmailTemplate?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct);

    /// <summary>Read-only lookup — for the detail endpoint and the send flow's ownership/status check.</summary>
    Task<EmailTemplate?> GetByPublicIdAsync(Guid publicId, CancellationToken ct);

    /// <summary>All of a creator's templates, Active first then by name.</summary>
    Task<List<EmailTemplate>> ListByCreatorIdAsync(int creatorId, CancellationToken ct);
}
