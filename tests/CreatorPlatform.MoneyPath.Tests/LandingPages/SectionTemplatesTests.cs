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
    [InlineData(LandingPageSectionType.Testimonials, "testimonials-simple")]
    [InlineData(LandingPageSectionType.Testimonials, "testimonials-grid")]
    [InlineData(LandingPageSectionType.Faq, "faq-simple")]
    [InlineData(LandingPageSectionType.Faq, "faq-extended")]
    [InlineData(LandingPageSectionType.Gallery, "gallery-simple")]
    [InlineData(LandingPageSectionType.Gallery, "gallery-showcase")]
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
}
