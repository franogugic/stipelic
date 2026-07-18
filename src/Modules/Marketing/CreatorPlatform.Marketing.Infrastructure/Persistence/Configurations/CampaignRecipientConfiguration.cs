using CreatorPlatform.Marketing.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Marketing.Infrastructure.Persistence.Configurations;

public sealed class CampaignRecipientConfiguration : IEntityTypeConfiguration<CampaignRecipient>
{
    public void Configure(EntityTypeBuilder<CampaignRecipient> builder)
    {
        builder.ToTable("campaign_recipients", "marketing");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd();

        builder.Property(r => r.CampaignId)
            .IsRequired();

        builder.Property(r => r.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.HasIndex(r => new { r.CampaignId, r.Email })
            .IsUnique();

        builder.HasOne<Campaign>()
            .WithMany()
            .HasForeignKey(r => r.CampaignId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
