using CreatorPlatform.Email.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Email.Infrastructure.Persistence.Configurations;

public sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("email_outbox_messages", "email");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Purpose)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(message => message.CorrelationKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(message => message.ToEmail)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(message => message.Subject)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(message => message.HtmlBody)
            .IsRequired();

        builder.Property(message => message.PlainTextBody)
            .IsRequired();

        builder.Property(message => message.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(message => message.RetryCount)
            .IsRequired();

        builder.Property(message => message.NextAttemptAt)
            .IsRequired();

        builder.Property(message => message.ProcessingExpiresAt);

        builder.Property(message => message.LastError)
            .HasMaxLength(1000);

        builder.Property(message => message.CreatedAt)
            .IsRequired();

        builder.HasIndex(message => new { message.Status, message.NextAttemptAt });
        builder.HasIndex(message => new { message.Status, message.ProcessingExpiresAt });
        builder.HasIndex(message => new { message.Purpose, message.CorrelationKey, message.Status });
    }
}
