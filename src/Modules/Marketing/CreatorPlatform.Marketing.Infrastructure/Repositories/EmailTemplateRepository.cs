using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Templates;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Repositories;

public sealed class EmailTemplateRepository : IEmailTemplateRepository
{
    private readonly CreatorPlatformDbContext _context;

    public EmailTemplateRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EmailTemplate template, CancellationToken ct)
    {
        await _context.Set<EmailTemplate>().AddAsync(template, ct);
    }

    public async Task<EmailTemplate?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct)
    {
        return await _context.Set<EmailTemplate>()
            .FirstOrDefaultAsync(t => t.PublicId == publicId, ct);
    }

    public async Task<EmailTemplate?> GetByPublicIdAsync(Guid publicId, CancellationToken ct)
    {
        return await _context.Set<EmailTemplate>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == publicId, ct);
    }

    public async Task<List<EmailTemplate>> ListByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        return await _context.Set<EmailTemplate>()
            .AsNoTracking()
            .Where(t => t.CreatorId == creatorId)
            .OrderBy(t => t.Status) // stored as string; "Active" sorts before "Archived" alphabetically too
            .ThenBy(t => t.Name)
            .ToListAsync(ct);
    }
}
