using CreatorPlatform.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Payments.Infrastructure.Persistence.Configurations;

public sealed class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.ToTable("processed_webhook_events", "payments");

        builder.HasKey(e => e.EventId);

        builder.Property(e => e.EventId)
            .HasMaxLength(255);

        builder.Property(e => e.Provider)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ClaimedAt)
            .IsRequired();

        builder.Property(e => e.ProcessedAt);

        // The retention clean-up deletes processed rows by age.
        builder.HasIndex(e => e.ProcessedAt);
    }
}
