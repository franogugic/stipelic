using CreatorPlatform.Marketing.Domain.Mail;

namespace CreatorPlatform.Marketing.Domain.Templates;

/// <summary>A reusable mail template a creator authors once and sends multiple times — a
/// <see cref="Campaigns.Campaign"/> row snapshots this content at the moment of send, so editing or
/// archiving a template never changes the history of what was already sent.</summary>
public sealed class EmailTemplate
{
    public const int MaxNameLength = 100;

    private EmailTemplate()
    {
    }

    private EmailTemplate(
        Guid publicId,
        int creatorId,
        string name,
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        CreatorId = creatorId;
        Name = name;
        Subject = subject;
        BodyText = bodyText;
        CtaLabel = ctaLabel;
        CtaUrl = ctaUrl;
        Status = EmailTemplateStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static EmailTemplate Create(
        int creatorId,
        string name,
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        DateTimeOffset createdAt)
    {
        ValidateName(name);
        MailContentRules.Validate(subject, bodyText, ctaLabel, ctaUrl);

        return new EmailTemplate(
            Guid.NewGuid(),
            creatorId,
            name.Trim(),
            subject.Trim(),
            bodyText,
            ctaLabel?.Trim(),
            ctaUrl?.Trim(),
            createdAt);
    }

    public void Update(
        string name,
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        DateTimeOffset updatedAt)
    {
        if (Status != EmailTemplateStatus.Active)
            throw new InvalidOperationException("Cannot update an archived template.");

        ValidateName(name);
        MailContentRules.Validate(subject, bodyText, ctaLabel, ctaUrl);

        Name = name.Trim();
        Subject = subject.Trim();
        BodyText = bodyText;
        CtaLabel = ctaLabel?.Trim();
        CtaUrl = ctaUrl?.Trim();
        UpdatedAt = updatedAt;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        if (Status == EmailTemplateStatus.Archived)
            throw new InvalidOperationException("This template is already archived.");

        Status = EmailTemplateStatus.Archived;
        UpdatedAt = archivedAt;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"Name must be at most {MaxNameLength} characters.", nameof(name));
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string BodyText { get; private set; } = string.Empty;

    public string? CtaLabel { get; private set; }

    public string? CtaUrl { get; private set; }

    public EmailTemplateStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
