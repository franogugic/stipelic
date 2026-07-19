using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

/// <summary>Guid.ToString() (default "D" format) is always exactly 36 characters, so the campaign public
/// id can be recovered from CorrelationKey ("{campaignPublicId}:{recipientId}") via a fixed-length
/// substring — no string splitting/parsing needed, and it translates to plain SQL SUBSTRING.</summary>
public sealed class CampaignProgressProvider : ICampaignProgressProvider
{
    private const int GuidStringLength = 36;

    private readonly CreatorPlatformDbContext _context;

    public CampaignProgressProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<Guid, CampaignProgressDto>> GetProgressAsync(
        IReadOnlyCollection<Guid> campaignPublicIds, CancellationToken ct)
    {
        var result = campaignPublicIds.ToDictionary(id => id, _ => new CampaignProgressDto(0, 0));
        if (campaignPublicIds.Count == 0)
            return result;

        var prefixes = campaignPublicIds.Select(id => id.ToString() + ":%").ToList();

        // Single aggregate query for every requested campaign — the WHERE clause ORs together one LIKE
        // per campaign (translated to SQL, not evaluated client-side), and GROUP BY recovers which
        // campaign + status each row belongs to, so this never runs once per campaign.
        var rows = await _context.Set<EmailOutboxMessage>()
            .AsNoTracking()
            .Where(m => m.Purpose == EmailOutboxMessagePurpose.CampaignBroadcast &&
                        prefixes.Any(p => EF.Functions.Like(m.CorrelationKey, p)))
            .GroupBy(m => new { CampaignPublicId = m.CorrelationKey.Substring(0, GuidStringLength), m.Status })
            .Select(g => new { g.Key.CampaignPublicId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            if (!Guid.TryParse(row.CampaignPublicId, out var campaignPublicId) || !result.ContainsKey(campaignPublicId))
                continue;

            var current = result[campaignPublicId];
            result[campaignPublicId] = row.Status switch
            {
                EmailOutboxMessageStatus.Sent => current with { SentCount = current.SentCount + row.Count },
                EmailOutboxMessageStatus.Failed => current with { FailedCount = current.FailedCount + row.Count },
                _ => current
            };
        }

        return result;
    }
}
