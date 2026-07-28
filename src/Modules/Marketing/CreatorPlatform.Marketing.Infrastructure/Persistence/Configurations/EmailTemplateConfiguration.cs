using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Marketing.Domain.Mail;
using CreatorPlatform.Marketing.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Marketing.Infrastructure.Persistence.Configurations;

public sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("email_templates", "marketing");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd();

        builder.Property(t => t.PublicId)
            .IsRequired();

        builder.HasIndex(t => t.PublicId)
            .IsUnique();

        builder.Property(t => t.CreatorId)
            .IsRequired();

        builder.Property(t => t.Name)
            .HasMaxLength(EmailTemplate.MaxNameLength)
            .IsRequired();

        builder.Property(t => t.Subject)
            .HasMaxLength(MailContentRules.MaxSubjectLength)
            .IsRequired();

        builder.Property(t => t.BodyText)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(t => t.CtaLabel)
            .HasMaxLength(MailContentRules.MaxCtaLabelLength);

        builder.Property(t => t.CtaUrl)
            .HasMaxLength(MailContentRules.MaxCtaUrlLength);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .IsRequired();

        builder.HasIndex(t => new { t.CreatorId, t.Status });

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(t => t.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
