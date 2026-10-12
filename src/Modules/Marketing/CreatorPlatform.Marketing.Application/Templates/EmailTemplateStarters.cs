namespace CreatorPlatform.Marketing.Application.Templates;

public sealed class EmailTemplateStarter
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Subject { get; init; }
    public required string BodyText { get; init; }
    /// <summary>A suggested button label. The URL is always left for the creator to fill in, so a starter with a
    /// button can't be saved until it has one (MailContentRules pairs label and URL).</summary>
    public string? CtaLabel { get; init; }
    public string? CtaUrl { get; init; }
}

/// <summary>The starting points offered when a creator writes a new template, as designed in the prototype. Only
/// read when a template is created (the content is copied), so nothing stored refers to these keys. {Braces} are
/// reminders for the creator, not merge tags.</summary>
public static class EmailTemplateStarters
{
    public static IReadOnlyList<EmailTemplateStarter> All { get; } =
    [
        new EmailTemplateStarter
        {
            Key = "announcement",
            Name = "Announcement",
            Description = "Share news or a launch in three short paragraphs.",
            Subject = "Big news from {Brand}",
            BodyText = "Hi,\n\n[The news in one sentence.]\n\n[Why it matters to the reader.]\n\n[What to do next.]",
            CtaLabel = "Learn more"
        },
        new EmailTemplateStarter
        {
            Key = "newsletter",
            Name = "Newsletter",
            Description = "A friendly digest with a few highlights.",
            Subject = "This month at {Brand}",
            BodyText = "Hi,\n\nHere is what happened this month:\n\n• \n• \n• "
        },
        new EmailTemplateStarter
        {
            Key = "promotion",
            Name = "Promotion",
            Description = "A time-limited offer with a clear call to action.",
            Subject = "{Discount}% off — this week only",
            BodyText = "Hi,\n\nFor a few days only, {Product} is {Discount}% off.\n\n[One line on who it is for.]",
            CtaLabel = "Get the offer"
        },
        new EmailTemplateStarter
        {
            Key = "welcome",
            Name = "Welcome",
            Description = "Say hello to new subscribers and set expectations.",
            Subject = "Welcome to {Brand}",
            BodyText = "Hi,\n\nThanks for joining! Here is what you can expect from me:\n\n[How often you write and what about.]"
        }
    ];
}
