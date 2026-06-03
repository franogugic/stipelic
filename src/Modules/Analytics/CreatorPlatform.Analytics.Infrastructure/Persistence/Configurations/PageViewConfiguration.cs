using CreatorPlatform.Analytics.Domain.PageViews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Analytics.Infrastructure.Persistence.Configurations;

public sealed class PageViewConfiguration : IEntityTypeConfiguration<PageView>
{
    public void Configure(EntityTypeBuilder<PageView> builder)
    {
        builder.ToTable("page_views", "analytics");

        builder.HasKey(pv => pv.Id);

        builder.Property(pv => pv.Id)
            .ValueGeneratedNever();

        builder.Property(pv => pv.LandingPageId)
            .IsRequired();

        builder.Property(pv => pv.VisitorId)
            .IsRequired();

        builder.Property(pv => pv.ViewedAt)
            .IsRequired();

        // Dedup check: postoji li view za ovog visitora na ovoj stranici u zadnjih 24h?
        builder.HasIndex(pv => new { pv.LandingPageId, pv.VisitorId, pv.ViewedAt });

        // Analytics queriji: koliko viewova/unika za stranicu u nekom periodu?
        builder.HasIndex(pv => new { pv.LandingPageId, pv.ViewedAt });
    }
}
