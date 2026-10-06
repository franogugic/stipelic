using CreatorPlatform.LandingPages.Domain.LandingPages;

namespace CreatorPlatform.LandingPages.Application.Templates;

public sealed class SectionTemplate
{
    /// <summary>"{type}-{variant}", e.g. "hero-split" — unique across all templates.</summary>
    public string Key => $"{TypeSlug(Type)}-{Variant}";
    public required LandingPageSectionType Type { get; init; }
    /// <summary>The layout id stored on the section (<c>LandingPageSection.Variant</c>).</summary>
    public required string Variant { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ContentJson { get; init; }
    /// <summary>Null = the page default (follows the page's light / dark theme).</summary>
    public string? DefaultBackgroundColor { get; init; }

    private static string TypeSlug(LandingPageSectionType type) => type switch
    {
        LandingPageSectionType.ProductDetails => "product",
        _ => type.ToString().ToLowerInvariant()
    };
}

/// <summary>Every section type with its two layouts. The first variant of a type is its default: new pages
/// start with it, and sections saved before variants existed were backfilled with it.</summary>
public static class SectionTemplates
{
    public const int VariantMaxLength = 40;

    public static IReadOnlyList<SectionTemplate> All { get; } =
    [
        new SectionTemplate
        {
            Type = LandingPageSectionType.Navbar,
            Variant = "simple",
            Name = "Simple",
            Description = "Logo left, links right",
            ContentJson = """{"brandName":"My Brand","links":[]}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Navbar,
            Variant = "centered",
            Name = "Centered",
            Description = "Logo centred above the links",
            ContentJson = """{"brandName":"My Brand","links":[]}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Hero,
            Variant = "split",
            Name = "Split with image",
            Description = "Text left, image right",
            ContentJson = """{"heading":"Transform Your Business Today","subheading":"Join thousands of creators who trust our platform.","ctaText":"Start now","imageUrl":null,"imageAlt":""}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Hero,
            Variant = "centered",
            Name = "Centered",
            Description = "Big centred headline, no image",
            ContentJson = """{"heading":"Welcome","subheading":"Tell your story here.","ctaText":"Get started","imageUrl":null,"imageAlt":""}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Features,
            Variant = "numbered",
            Name = "Numbered columns",
            Description = "Three columns with large numbers",
            ContentJson = """{"heading":"Why choose this","items":[{"title":"Fast","description":"Lightning fast results from day one."},{"title":"Secure","description":"Your data is always protected."},{"title":"Simple","description":"No learning curve required."}]}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Features,
            Variant = "checklist",
            Name = "Checklist",
            Description = "Two columns with check marks",
            ContentJson = """{"heading":"Everything you need","items":[{"title":"Feature one","description":"Description one."},{"title":"Feature two","description":"Description two."},{"title":"Feature three","description":"Description three."},{"title":"Feature four","description":"Description four."}]}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.ProductDetails,
            Variant = "image-left",
            Name = "Image beside text",
            Description = "Product image with the details next to it",
            ContentJson = """{"heading":"About this product","description":"Describe your product here.","showPrice":true,"bullets":[],"imageUrl":null,"imageAlt":""}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.ProductDetails,
            Variant = "card",
            Name = "Price card",
            Description = "Details with a highlighted price card",
            ContentJson = """{"heading":"Everything you get","description":"A comprehensive breakdown of what's included.","showPrice":true,"bullets":["Instant access after purchase","Lifetime updates","Step-by-step guidance"],"imageUrl":null,"imageAlt":""}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Testimonials,
            Variant = "cards",
            Name = "Quote cards",
            Description = "Three quotes on a dark band",
            ContentJson = """{"heading":"Loved by creators everywhere","items":[{"quote":"The best decision I made this year.","author":"Morgan Blake","role":"Creator"},{"quote":"Support is fantastic and the product delivers.","author":"Sam Chen","role":"Customer"},{"quote":"Exactly what I needed to get started.","author":"Taylor Reed","role":"Creator"}]}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Testimonials,
            Variant = "single",
            Name = "Single quote",
            Description = "One large quote",
            ContentJson = """{"heading":"","items":[{"quote":"This changed how I run my business.","author":"Jamie Lee","role":"Creator"}]}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Faq,
            Variant = "accordion",
            Name = "Accordion",
            Description = "Questions that expand",
            ContentJson = """{"heading":"Frequently asked questions","items":[{"question":"How does this work?","answer":"You get instant access after purchase."},{"question":"Can I get a refund?","answer":"Yes, within 14 days of purchase."}]}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Faq,
            Variant = "columns",
            Name = "Two columns",
            Description = "Heading left, questions right",
            ContentJson = """{"heading":"Questions? We've got answers","items":[{"question":"How does this work?","answer":"You get instant access after purchase."},{"question":"Can I get a refund?","answer":"Yes, within 14 days of purchase."},{"question":"Is support included?","answer":"Yes, email support is included with every purchase."}]}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Gallery,
            Variant = "grid",
            Name = "Even grid",
            Description = "Same-size images in rows of three",
            ContentJson = """{"heading":"Gallery","imageUrls":[],"imageAlts":[]}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Gallery,
            Variant = "mosaic",
            Name = "Mosaic",
            Description = "One large image with smaller ones",
            ContentJson = """{"heading":"See it in action","imageUrls":[],"imageAlts":[]}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Cta,
            Variant = "banner",
            Name = "Colour banner",
            Description = "Full-width band in your brand colour",
            ContentJson = """{"heading":"Ready to get started?","subheading":"Don't miss out. Join today.","buttonText":"Get started now"}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Cta,
            Variant = "card",
            Name = "Centered card",
            Description = "A calm card in the middle of the page",
            ContentJson = """{"heading":"Interested?","subheading":"No pressure, take your time.","buttonText":"Learn more"}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Footer,
            Variant = "simple",
            Name = "Simple",
            Description = "Copyright left, credit right",
            ContentJson = """{"copyright":"© My Brand. All rights reserved."}"""
        },
        new SectionTemplate
        {
            Type = LandingPageSectionType.Footer,
            Variant = "centered",
            Name = "Centered",
            Description = "Everything centred",
            ContentJson = """{"copyright":"© My Brand. All rights reserved."}"""
        }
    ];

    public static SectionTemplate? Find(LandingPageSectionType type, string key)
        => All.FirstOrDefault(t => t.Type == type && t.Key == key);

    public static SectionTemplate? FindVariant(LandingPageSectionType type, string variant)
        => All.FirstOrDefault(t => t.Type == type && t.Variant == variant);

    public static SectionTemplate GetDefault(LandingPageSectionType type)
        => All.First(t => t.Type == type);
}
