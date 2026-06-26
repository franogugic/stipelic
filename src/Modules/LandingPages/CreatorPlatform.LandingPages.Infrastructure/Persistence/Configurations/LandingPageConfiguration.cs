using CreatorPlatform.LandingPages.Domain.LandingPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.LandingPages.Infrastructure.Persistence.Configurations;

public sealed class LandingPageConfiguration : IEntityTypeConfiguration<LandingPage>
{
    public void Configure(EntityTypeBuilder<LandingPage> builder)
    {
        builder.ToTable("landing_pages", "landing_pages");

        builder.HasKey(lp => lp.Id);

        builder.Property(lp => lp.Id)
            .ValueGeneratedOnAdd();

        builder.Property(lp => lp.PublicId)
            .IsRequired();

        builder.HasIndex(lp => lp.PublicId)
            .IsUnique();

        builder.Property(lp => lp.CreatorId)
            .IsRequired();

        builder.Property(lp => lp.ProductId);

        builder.Property(lp => lp.Title)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(lp => lp.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(lp => new { lp.CreatorId, lp.Slug })
            .IsUnique()
            .HasFilter("\"Status\" != 'Archived'");

        builder.Property(lp => lp.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(lp => lp.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(lp => lp.CustomDomain)
            .HasMaxLength(253);

        builder.HasIndex(lp => lp.CustomDomain)
            .IsUnique()
            .HasFilter("\"CustomDomain\" IS NOT NULL");

        builder.HasIndex(lp => new { lp.CreatorId, lp.Status });

        builder.Property(lp => lp.CreatedAt)
            .IsRequired();

        builder.Property(lp => lp.UpdatedAt)
            .IsRequired();
    }
}
