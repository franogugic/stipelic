namespace CreatorPlatform.Marketing.Application.Templates;

public sealed class EmailTemplateStarter
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Subject { get; init; }
    public required string BodyText { get; init; }
    public string? CtaLabel { get; init; }
    public string? CtaUrl { get; init; }
}

public static class EmailTemplateStarters
{
    public static IReadOnlyList<EmailTemplateStarter> All { get; } =
    [
        new EmailTemplateStarter
        {
            Key = "product-launch",
            Name = "Product launch",
            Subject = "It's here — my new product just launched",
            BodyText = """
                Hey there,

                I've been working on something new and it's finally ready — my latest product is live today.

                If you've been waiting for this, now's the time to check it out. I built it to solve a problem I know a lot of you have run into, and I think you're going to like what I put together.

                Take a look and let me know what you think.
                """
        },
        new EmailTemplateStarter
        {
            Key = "welcome-new-subscriber",
            Name = "Welcome new subscriber",
            Subject = "Welcome — glad you're here",
            BodyText = """
                Hi, and welcome!

                Thanks for signing up. I send emails like this one when I have something worth sharing — new products, useful updates, or the occasional offer for people on this list.

                No spam, no noise — just the good stuff. Glad to have you here.
                """
        },
        new EmailTemplateStarter
        {
            Key = "limited-time-discount",
            Name = "Limited-time discount",
            Subject = "A discount, but only for a few days",
            BodyText = """
                Hey,

                For a short time, I'm offering a discount on one of my products. If it's something you've had your eye on, this is a good moment to grab it.

                This offer won't stick around, so don't wait too long if you're interested.
                """
        },
        new EmailTemplateStarter
        {
            Key = "win-back-inactive",
            Name = "Win-back inactive contacts",
            Subject = "It's been a while",
            BodyText = """
                Hey,

                It's been a bit since we last connected, and I wanted to check in. I've added new things since you last looked, and I'd love for you to take another look.

                If you're still interested in what I do, here's a good place to pick back up.
                """
        }
    ];
}
