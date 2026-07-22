using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Marketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Marketing.Infrastructure.Persistence.Configurations;

public sealed class ContactSummaryConfiguration : IEntityTypeConfiguration<ContactSummary>
{
    public void Configure(EntityTypeBuilder<ContactSummary> builder)
    {
        builder.ToTable("contact_summaries", "marketing");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.CreatorId)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(c => c.FirstCapturedAt)
            .IsRequired();

        builder.Property(c => c.LastCapturedAt)
            .IsRequired();

        builder.Property(c => c.SourceLandingPageIds)
            .HasColumnType("integer[]")
            .IsRequired();

        builder.HasIndex(c => new { c.CreatorId, c.Email })
            .IsUnique();

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(c => c.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
