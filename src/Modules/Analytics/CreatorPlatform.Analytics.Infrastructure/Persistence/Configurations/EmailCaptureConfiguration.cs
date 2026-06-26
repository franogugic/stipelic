using CreatorPlatform.Analytics.Domain.EmailCaptures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Analytics.Infrastructure.Persistence.Configurations;

public sealed class EmailCaptureConfiguration : IEntityTypeConfiguration<EmailCapture>
{
    public void Configure(EntityTypeBuilder<EmailCapture> builder)
    {
        builder.ToTable("email_captures", "analytics");

        builder.HasKey(ec => ec.Id);

        builder.Property(ec => ec.Id)
            .ValueGeneratedNever();

        builder.Property(ec => ec.LandingPageId)
            .IsRequired();

        builder.Property(ec => ec.ProductId);

        builder.Property(ec => ec.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(ec => ec.CapturedAt)
            .IsRequired();

        // Deduplication — isti email ne može dva puta na istoj stranici
        builder.HasIndex(ec => new { ec.LandingPageId, ec.Email })
            .IsUnique();

        builder.HasIndex(ec => new { ec.LandingPageId, ec.CapturedAt });
    }
}
