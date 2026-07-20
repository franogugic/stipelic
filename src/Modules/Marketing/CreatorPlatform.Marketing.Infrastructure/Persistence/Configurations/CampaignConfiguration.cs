using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Mail;
using CreatorPlatform.Marketing.Domain.Templates;
using CreatorPlatform.Products.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Marketing.Infrastructure.Persistence.Configurations;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns", "marketing");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.PublicId)
            .IsRequired();

        builder.HasIndex(c => c.PublicId)
            .IsUnique();

        builder.Property(c => c.CreatorId)
            .IsRequired();

        builder.Property(c => c.TemplateId);

        builder.Property(c => c.Subject)
            .HasMaxLength(MailContentRules.MaxSubjectLength)
            .IsRequired();

        builder.Property(c => c.BodyText)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.CtaLabel)
            .HasMaxLength(MailContentRules.MaxCtaLabelLength);

        builder.Property(c => c.CtaUrl)
            .HasMaxLength(MailContentRules.MaxCtaUrlLength);

        builder.Property(c => c.AudienceType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.LandingPageId);

        builder.Property(c => c.ProductId);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.RecipientCount)
            .IsRequired();

        builder.Property(c => c.QueuedAt);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        builder.HasIndex(c => new { c.CreatorId, c.CreatedAt });

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_campaigns_Audience_Matches_Fk",
            "(\"AudienceType\" = 'LandingPage' AND \"LandingPageId\" IS NOT NULL AND \"ProductId\" IS NULL) " +
            "OR (\"AudienceType\" = 'Product' AND \"ProductId\" IS NOT NULL AND \"LandingPageId\" IS NULL)"));

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(c => c.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<LandingPage>()
            .WithMany()
            .HasForeignKey(c => c.LandingPageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EmailTemplate>()
            .WithMany()
            .HasForeignKey(c => c.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
