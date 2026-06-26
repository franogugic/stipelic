using CreatorPlatform.LandingPages.Domain.LandingPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.LandingPages.Infrastructure.Persistence.Configurations;

public sealed class LandingPageSectionConfiguration : IEntityTypeConfiguration<LandingPageSection>
{
    public void Configure(EntityTypeBuilder<LandingPageSection> builder)
    {
        builder.ToTable("landing_page_sections", "landing_pages");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();

        builder.Property(s => s.PublicId)
            .IsRequired();

        builder.HasIndex(s => s.PublicId)
            .IsUnique();

        builder.Property(s => s.LandingPageId)
            .IsRequired();

        builder.HasOne(s => s.LandingPage)
            .WithMany()
            .HasForeignKey(s => s.LandingPageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.SortOrder)
            .IsRequired();

        builder.HasIndex(s => new { s.LandingPageId, s.SortOrder });

        builder.Property(s => s.BackgroundColor)
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(s => s.ContentJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();
    }
}
