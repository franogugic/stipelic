using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Templates;
using CreatorPlatform.Marketing.Domain.Templates;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class EmailTemplateService : IEmailTemplateService
{
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IEmailTemplateRepository _templateRepository;
    private readonly IMarketingUnitOfWork _unitOfWork;

    public EmailTemplateService(
        ICreatorContextProvider creatorContextProvider,
        IEmailTemplateRepository templateRepository,
        IMarketingUnitOfWork unitOfWork)
    {
        _creatorContextProvider = creatorContextProvider;
        _templateRepository = templateRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<EmailTemplateDto>> ListAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var templates = await _templateRepository.ListByCreatorIdAsync(context.CreatorId, ct);

        return templates.Select(ToDto).ToList();
    }

    public async Task<EmailTemplateDto> GetAsync(string slug, int ownerUserId, Guid templatePublicId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);
        var template = await _templateRepository.GetByPublicIdAsync(templatePublicId, ct);
        if (template is null || template.CreatorId != context.CreatorId)
            throw new NotFoundException("Template not found.");

        return ToDto(template);
    }

    public async Task<EmailTemplateDto> CreateAsync(
        string slug, int ownerUserId, SaveEmailTemplateRequestDto request, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        EmailTemplate template;
        try
        {
            template = EmailTemplate.Create(
                context.CreatorId,
                request.Name,
                request.Subject,
                request.BodyText,
                request.CtaLabel,
                request.CtaUrl,
                DateTimeOffset.UtcNow);
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        await _templateRepository.AddAsync(template, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(template);
    }

    public async Task<EmailTemplateDto> UpdateAsync(
        string slug, int ownerUserId, Guid templatePublicId, SaveEmailTemplateRequestDto request, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);
        var template = await GetOwnedTemplateForUpdateAsync(context.CreatorId, templatePublicId, ct);

        try
        {
            template.Update(request.Name, request.Subject, request.BodyText, request.CtaLabel, request.CtaUrl, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(template);
    }

    public async Task<EmailTemplateDto> ArchiveAsync(string slug, int ownerUserId, Guid templatePublicId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);
        var template = await GetOwnedTemplateForUpdateAsync(context.CreatorId, templatePublicId, ct);

        try
        {
            template.Archive(DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(template);
    }

    public List<EmailTemplateStarterDto> GetStarters()
    {
        return EmailTemplateStarters.All
            .Select(t => new EmailTemplateStarterDto(t.Key, t.Name, t.Subject, t.BodyText, t.CtaLabel, t.CtaUrl))
            .ToList();
    }

    private async Task<MarketingCreatorContext> GetCreatorContextAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

        return context;
    }

    private async Task<EmailTemplate> GetOwnedTemplateForUpdateAsync(int creatorId, Guid templatePublicId, CancellationToken ct)
    {
        var template = await _templateRepository.GetByPublicIdForUpdateAsync(templatePublicId, ct);
        if (template is null || template.CreatorId != creatorId)
            throw new NotFoundException("Template not found.");

        return template;
    }

    private static EmailTemplateDto ToDto(EmailTemplate template)
    {
        return new EmailTemplateDto(
            template.PublicId,
            template.Name,
            template.Subject,
            template.BodyText,
            template.CtaLabel,
            template.CtaUrl,
            template.Status.ToString(),
            template.CreatedAt,
            template.UpdatedAt);
    }
}
