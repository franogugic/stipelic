using CreatorPlatform.Marketing.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Marketing.Infrastructure.Persistence.Configurations;

public sealed class CampaignLinkClickConfiguration : IEntityTypeConfiguration<CampaignLinkClick>
{
    public void Configure(EntityTypeBuilder<CampaignLinkClick> builder)
    {
        builder.ToTable("campaign_link_clicks", "marketing");

        // The click upsert's conflict target: one row per (link, recipient).
        builder.HasKey(c => new { c.LinkId, c.RecipientId });

        builder.Property(c => c.FirstClickedAt)
            .IsRequired();

        builder.Property(c => c.LastClickedAt)
            .IsRequired();

        builder.Property(c => c.ClickCount)
            .IsRequired();

        // Recipient deletes (e.g. a contact's data removed) cascade without scanning the whole table.
        builder.HasIndex(c => c.RecipientId);

        builder.HasOne<CampaignLink>()
            .WithMany()
            .HasForeignKey(c => c.LinkId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<CampaignRecipient>()
            .WithMany()
            .HasForeignKey(c => c.RecipientId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
