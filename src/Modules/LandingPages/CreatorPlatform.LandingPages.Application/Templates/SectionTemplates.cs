using CreatorPlatform.LandingPages.Domain.LandingPages;

namespace CreatorPlatform.LandingPages.Application.Templates;

public sealed class SectionTemplate
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required LandingPageSectionType Type { get; init; }
    public required string ContentJson { get; init; }
    public required string DefaultBackgroundColor { get; init; }
}

public static class SectionTemplates
{
    public static IReadOnlyList<SectionTemplate> All { get; } =
    [
        new SectionTemplate
        {
            Key = "navbar-simple",
            Name = "Simple",
            Type = LandingPageSectionType.Navbar,
            DefaultBackgroundColor = "#ffffff",
            ContentJson = """{"brandName":"My Brand","links":[]}"""
        },
        new SectionTemplate
        {
            Key = "navbar-dark",
            Name = "Dark",
            Type = LandingPageSectionType.Navbar,
            DefaultBackgroundColor = "#111827",
            ContentJson = """{"brandName":"My Brand","links":[]}"""
        },
        new SectionTemplate
        {
            Key = "hero-minimal",
            Name = "Minimal",
            Type = LandingPageSectionType.Hero,
            DefaultBackgroundColor = "#ffffff",
            ContentJson = """{"heading":"Welcome","subheading":"Tell your story here.","ctaText":"Get started"}"""
        },
        new SectionTemplate
        {
            Key = "hero-bold",
            Name = "Bold",
            Type = LandingPageSectionType.Hero,
            DefaultBackgroundColor = "#111827",
            ContentJson = """{"heading":"Transform Your Business Today","subheading":"Join thousands of creators who trust our platform.","ctaText":"Start now"}"""
        },
        new SectionTemplate
        {
            Key = "features-three",
            Name = "3 benefits",
            Type = LandingPageSectionType.Features,
            DefaultBackgroundColor = "#f9fafb",
            ContentJson = """{"heading":"Why choose this","items":[{"title":"Fast","description":"Lightning fast results from day one."},{"title":"Secure","description":"Your data is always protected."},{"title":"Simple","description":"No learning curve required."}]}"""
        },
        new SectionTemplate
        {
            Key = "features-five",
            Name = "5 benefits",
            Type = LandingPageSectionType.Features,
            DefaultBackgroundColor = "#f9fafb",
            ContentJson = """{"heading":"Everything you need","items":[{"title":"Feature one","description":"Description one."},{"title":"Feature two","description":"Description two."},{"title":"Feature three","description":"Description three."},{"title":"Feature four","description":"Description four."},{"title":"Feature five","description":"Description five."}]}"""
        },
        new SectionTemplate
        {
            Key = "product-simple",
            Name = "Simple",
            Type = LandingPageSectionType.ProductDetails,
            DefaultBackgroundColor = "#ffffff",
            ContentJson = """{"heading":"About this product","description":"Describe your product here.","showPrice":true,"bullets":[]}"""
        },
        new SectionTemplate
        {
            Key = "product-detailed",
            Name = "Detailed",
            Type = LandingPageSectionType.ProductDetails,
            DefaultBackgroundColor = "#ffffff",
            ContentJson = """{"heading":"Everything you get","description":"A comprehensive breakdown of what's included.","showPrice":true,"bullets":["Instant access after purchase","Lifetime updates","Step-by-step guidance"]}"""
        },
        new SectionTemplate
        {
            Key = "cta-soft",
            Name = "Soft",
            Type = LandingPageSectionType.Cta,
            DefaultBackgroundColor = "#f9fafb",
            ContentJson = """{"heading":"Interested?","subheading":"No pressure, take your time.","buttonText":"Learn more"}"""
        },
        new SectionTemplate
        {
            Key = "cta-strong",
            Name = "Strong",
            Type = LandingPageSectionType.Cta,
            DefaultBackgroundColor = "#111827",
            ContentJson = """{"heading":"Ready to get started?","subheading":"Don't miss out. Join today.","buttonText":"Get started now"}"""
        },
        new SectionTemplate
        {
            Key = "footer-simple",
            Name = "Simple",
            Type = LandingPageSectionType.Footer,
            DefaultBackgroundColor = "#ffffff",
            ContentJson = """{"copyright":"© 2025 My Brand. All rights reserved."}"""
        },
        new SectionTemplate
        {
            Key = "footer-dark",
            Name = "Dark",
            Type = LandingPageSectionType.Footer,
            DefaultBackgroundColor = "#111827",
            ContentJson = """{"copyright":"© 2025 My Brand. All rights reserved."}"""
        },
        new SectionTemplate
        {
            Key = "testimonials-simple",
            Name = "Simple",
            Type = LandingPageSectionType.Testimonials,
            DefaultBackgroundColor = "#ffffff",
            ContentJson = """{"heading":"What people are saying","items":[{"quote":"This changed how I run my business.","author":"Jamie Lee","role":"Creator"},{"quote":"Simple to use and it just works.","author":"Alex Rivera","role":"Customer"}]}"""
        },
        new SectionTemplate
        {
            Key = "testimonials-grid",
            Name = "Grid",
            Type = LandingPageSectionType.Testimonials,
            DefaultBackgroundColor = "#f9fafb",
            ContentJson = """{"heading":"Loved by creators everywhere","items":[{"quote":"The best decision I made this year.","author":"Morgan Blake","role":"Creator"},{"quote":"Support is fantastic and the product delivers.","author":"Sam Chen","role":"Customer"},{"quote":"Exactly what I needed to get started.","author":"Taylor Reed","role":"Creator"}]}"""
        },
        new SectionTemplate
        {
            Key = "faq-simple",
            Name = "Simple",
            Type = LandingPageSectionType.Faq,
            DefaultBackgroundColor = "#ffffff",
            ContentJson = """{"heading":"Frequently asked questions","items":[{"question":"How does this work?","answer":"You get instant access after purchase."},{"question":"Can I get a refund?","answer":"Yes, within 14 days of purchase."}]}"""
        },
        new SectionTemplate
        {
            Key = "faq-extended",
            Name = "Extended",
            Type = LandingPageSectionType.Faq,
            DefaultBackgroundColor = "#f9fafb",
            ContentJson = """{"heading":"Questions? We've got answers","items":[{"question":"How does this work?","answer":"You get instant access after purchase."},{"question":"Can I get a refund?","answer":"Yes, within 14 days of purchase."},{"question":"Is support included?","answer":"Yes, email support is included with every purchase."}]}"""
        },
        new SectionTemplate
        {
            Key = "gallery-simple",
            Name = "Simple",
            Type = LandingPageSectionType.Gallery,
            DefaultBackgroundColor = "#ffffff",
            ContentJson = """{"heading":"Gallery","imageUrls":[]}"""
        },
        new SectionTemplate
        {
            Key = "gallery-showcase",
            Name = "Showcase",
            Type = LandingPageSectionType.Gallery,
            DefaultBackgroundColor = "#f9fafb",
            ContentJson = """{"heading":"See it in action","imageUrls":[]}"""
        }
    ];

    public static SectionTemplate? Find(LandingPageSectionType type, string key)
        => All.FirstOrDefault(t => t.Type == type && t.Key == key);

    public static SectionTemplate GetDefault(LandingPageSectionType type)
        => All.First(t => t.Type == type);
}
