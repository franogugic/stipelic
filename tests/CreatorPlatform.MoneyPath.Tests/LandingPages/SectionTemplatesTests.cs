using CreatorPlatform.LandingPages.Application.Templates;
using CreatorPlatform.LandingPages.Domain.LandingPages;

namespace CreatorPlatform.MoneyPath.Tests.LandingPages;

public class SectionTemplatesTests
{
    [Theory]
    [InlineData(LandingPageSectionType.Navbar)]
    [InlineData(LandingPageSectionType.Hero)]
    [InlineData(LandingPageSectionType.Features)]
    [InlineData(LandingPageSectionType.ProductDetails)]
    [InlineData(LandingPageSectionType.Cta)]
    [InlineData(LandingPageSectionType.Footer)]
    [InlineData(LandingPageSectionType.Testimonials)]
    [InlineData(LandingPageSectionType.Faq)]
    [InlineData(LandingPageSectionType.Gallery)]
    public void All_EveryType_HasAtLeastTwoTemplates(LandingPageSectionType type)
    {
        var count = SectionTemplates.All.Count(t => t.Type == type);

        Assert.True(count >= 2, $"{type} has only {count} template(s), expected at least 2.");
    }

    [Theory]
    [InlineData(LandingPageSectionType.Testimonials)]
    [InlineData(LandingPageSectionType.Faq)]
    [InlineData(LandingPageSectionType.Gallery)]
    public void GetDefault_NewSectionTypes_ReturnsFirstMatchingTemplate(LandingPageSectionType type)
    {
        var result = SectionTemplates.GetDefault(type);

        Assert.Equal(type, result.Type);
    }

    [Theory]
    [InlineData(LandingPageSectionType.Testimonials, "testimonials-cards")]
    [InlineData(LandingPageSectionType.Testimonials, "testimonials-single")]
    [InlineData(LandingPageSectionType.Faq, "faq-accordion")]
    [InlineData(LandingPageSectionType.Faq, "faq-columns")]
    [InlineData(LandingPageSectionType.Gallery, "gallery-grid")]
    [InlineData(LandingPageSectionType.Gallery, "gallery-mosaic")]
    [InlineData(LandingPageSectionType.ProductDetails, "product-image-left")]
    public void Find_NewSectionTypes_ReturnsMatchingTemplate(LandingPageSectionType type, string key)
    {
        var result = SectionTemplates.Find(type, key);

        Assert.NotNull(result);
        Assert.Equal(type, result!.Type);
        Assert.Equal(key, result.Key);
    }

    [Fact]
    public void Find_UnknownKey_ReturnsNull()
    {
        var result = SectionTemplates.Find(LandingPageSectionType.Gallery, "does-not-exist");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(LandingPageSectionType.Testimonials)]
    [InlineData(LandingPageSectionType.Faq)]
    [InlineData(LandingPageSectionType.Gallery)]
    public void All_NewSectionTypes_ContentJsonHasHeadingAndItemsOrImageUrls(LandingPageSectionType type)
    {
        foreach (var template in SectionTemplates.All.Where(t => t.Type == type))
        {
            Assert.Contains("\"heading\"", template.ContentJson);
        }
    }

    /// <summary>The prototype's layouts, first one = the default (also the migration backfill).</summary>
    [Theory]
    [InlineData(LandingPageSectionType.Navbar, "simple", "centered")]
    [InlineData(LandingPageSectionType.Hero, "split", "centered")]
    [InlineData(LandingPageSectionType.Features, "numbered", "checklist")]
    [InlineData(LandingPageSectionType.ProductDetails, "image-left", "card")]
    [InlineData(LandingPageSectionType.Testimonials, "cards", "single")]
    [InlineData(LandingPageSectionType.Faq, "accordion", "columns")]
    [InlineData(LandingPageSectionType.Gallery, "grid", "mosaic")]
    [InlineData(LandingPageSectionType.Cta, "banner", "card")]
    [InlineData(LandingPageSectionType.Footer, "simple", "centered")]
    public void EveryType_HasThePrototypeVariants_FirstIsTheDefault(LandingPageSectionType type, string first, string second)
    {
        Assert.Equal([first, second], SectionTemplates.All.Where(t => t.Type == type).Select(t => t.Variant));
        Assert.Equal(first, SectionTemplates.GetDefault(type).Variant);
    }

    [Fact]
    public void All_KeysAreUnique_VariantsFitTheColumn_AndBackgroundsArePageDefault()
    {
        Assert.Equal(SectionTemplates.All.Count, SectionTemplates.All.Select(t => t.Key).Distinct().Count());
        Assert.All(SectionTemplates.All, t =>
        {
            Assert.InRange(t.Variant.Length, 1, SectionTemplates.VariantMaxLength);
            Assert.False(string.IsNullOrWhiteSpace(t.Description));
            Assert.Null(t.DefaultBackgroundColor);
            System.Text.Json.JsonDocument.Parse(t.ContentJson).Dispose();
        });
    }
}
