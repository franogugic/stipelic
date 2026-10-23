using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Marketing.Infrastructure.Persistence.Configurations;

public sealed class CampaignLinkConfiguration : IEntityTypeConfiguration<CampaignLink>
{
    public void Configure(EntityTypeBuilder<CampaignLink> builder)
    {
        builder.ToTable("campaign_links", "marketing");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd();

        builder.Property(l => l.CampaignId)
            .IsRequired();

        builder.Property(l => l.Position)
            .IsRequired();

        builder.Property(l => l.Url)
            .HasMaxLength(MailContentRules.MaxCtaUrlLength)
            .IsRequired();

        builder.Property(l => l.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.Label)
            .HasMaxLength(MailContentRules.MaxCtaLabelLength);

        builder.Property(l => l.UniqueClickCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(l => l.TotalClickCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .IsRequired();

        builder.HasIndex(l => new { l.CampaignId, l.Url })
            .IsUnique();

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_campaign_links_Label_Matches_Kind",
            "(\"Kind\" = 'Button' AND \"Label\" IS NOT NULL) OR (\"Kind\" = 'Text' AND \"Label\" IS NULL)"));

        builder.HasOne<Campaign>()
            .WithMany()
            .HasForeignKey(l => l.CampaignId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
