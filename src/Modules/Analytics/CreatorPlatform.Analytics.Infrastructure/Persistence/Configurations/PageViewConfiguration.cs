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

        builder.Property(pv => pv.ViewedDate)
            .IsRequired();

        // Unique constraint — jedan view po visitoru po stranici po danu (UTC)
        builder.HasIndex(pv => new { pv.LandingPageId, pv.VisitorId, pv.ViewedDate })
            .IsUnique();

        // Analytics queriji: koliko viewova/unika za stranicu u nekom periodu?
        builder.HasIndex(pv => new { pv.LandingPageId, pv.ViewedAt });
    }
}
