using CreatorPlatform.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Payments.Infrastructure.Persistence.Configurations;

public sealed class WebhookFailureConfiguration : IEntityTypeConfiguration<WebhookFailure>
{
    public void Configure(EntityTypeBuilder<WebhookFailure> builder)
    {
        builder.ToTable("webhook_failures", "payments");

        builder.ToTable(t => t.HasCheckConstraint("CK_webhook_failures_Attempts_Positive", "\"Attempts\" >= 1"));

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .ValueGeneratedOnAdd();

        builder.Property(f => f.Provider)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(f => f.EventId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.Payload)
            .IsRequired();

        builder.Property(f => f.ErrorMessage)
            .IsRequired();

        builder.Property(f => f.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.OccurredAt)
            .IsRequired();

        builder.Property(f => f.LastFailedAt)
            .IsRequired();

        builder.Property(f => f.Attempts)
            .IsRequired();

        builder.Property(f => f.IsResolved)
            .IsRequired();

        builder.HasIndex(f => f.EventId);
        builder.HasIndex(f => new { f.IsResolved, f.LastFailedAt });
    }
}
