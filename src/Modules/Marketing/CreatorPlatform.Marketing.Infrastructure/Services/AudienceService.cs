using CreatorPlatform.Analytics.Domain.EmailCaptures;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

public sealed class AudienceService : IAudienceService
{
    private readonly CreatorPlatformDbContext _context;

    public AudienceService(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetAudienceCountAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId, CancellationToken ct)
    {
        return await BuildAudienceQuery(audienceType, landingPageId, productId, creatorId).CountAsync(ct);
    }

    public async Task<List<string>> GetAudienceEmailsAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId, CancellationToken ct)
    {
        return await BuildAudienceQuery(audienceType, landingPageId, productId, creatorId).ToListAsync(ct);
    }

    private IQueryable<string> BuildAudienceQuery(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId)
    {
        var unsubscribedEmails = _context.Set<Unsubscribe>()
            .AsNoTracking()
            .Where(u => u.CreatorId == creatorId)
            .Select(u => u.Email);

        IQueryable<string> captured;

        if (audienceType == CampaignAudienceType.LandingPage)
        {
            captured = _context.Set<EmailCapture>()
                .AsNoTracking()
                .Where(ec => ec.LandingPageId == landingPageId)
                .Select(ec => ec.Email);
        }
        else
        {
            // Product audience = captures attributed directly to the product OR captures on any landing
            // page whose ProductId is this product — EmailCapture.ProductId is normally populated
            // straight from the landing page at capture time, but this covers both fill paths (e.g. older
            // rows, or the product assignment changing after capture) via a set union (dedupes both sides).
            var direct = _context.Set<EmailCapture>()
                .AsNoTracking()
                .Where(ec => ec.ProductId == productId)
                .Select(ec => ec.Email);

            var viaLandingPage = _context.Set<EmailCapture>()
                .AsNoTracking()
                .Join(
                    _context.Set<LandingPage>().AsNoTracking(),
                    ec => ec.LandingPageId,
                    lp => lp.Id,
                    (ec, lp) => new { ec.Email, lp.ProductId })
                .Where(x => x.ProductId == productId)
                .Select(x => x.Email);

            captured = direct.Union(viaLandingPage);
        }

        return captured.Distinct().Except(unsubscribedEmails);
    }
}
