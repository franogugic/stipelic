using CreatorPlatform.Email.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Email.Infrastructure.Persistence.Configurations;

public sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public const string SentWithBodiesIndexName = "IX_email_outbox_messages_SentAt_WithBodies";

    public const string SentWithBodiesIndexFilter = "\"Status\" = 'Sent' AND \"HtmlBody\" IS NOT NULL";

    /// <summary>Messages the outbox may still send (or is sending).</summary>
    public const string UnsentFilter = "\"Status\" IN ('Pending', 'Processing')";

    /// <summary>Unsent campaign mail: the fair queue.</summary>
    public const string CampaignQueueFilter = "\"Purpose\" = 'CampaignBroadcast' AND " + UnsentFilter;

    /// <summary>Unsent transactional mail.</summary>
    public const string TransactionalQueueFilter = "\"Purpose\" <> 'CampaignBroadcast' AND " + UnsentFilter;

    public const string CampaignQueueIndexName = "IX_email_outbox_messages_CampaignQueue";
    public const string CreatorCampaignQueueIndexName = "IX_email_outbox_messages_CreatorCampaignQueue";
    public const string TransactionalQueueIndexName = "IX_email_outbox_messages_TransactionalQueue";

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

        // 254: the longest address a sign-up or a buyer can have (RFC 5321).
        builder.Property(message => message.ToEmail)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(message => message.Subject)
            .HasMaxLength(200)
            .IsRequired();

        // Nullable: dropped from sent mail after the retention period (maintenance); set on every message that can be sent.
        builder.Property(message => message.HtmlBody);

        builder.Property(message => message.PlainTextBody);

        builder.Property(message => message.ReplyTo)
            .HasMaxLength(254);

        builder.Property(message => message.ListUnsubscribeUrl)
            .HasMaxLength(2000);

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
            .HasMaxLength(EmailOutboxMessage.LastErrorMaxLength);

        builder.Property(message => message.CreatedAt)
            .IsRequired();

        // No FK to creators: the outbox stays decoupled from the modules that queue into it.
        builder.Property(message => message.CreatorId);

        builder.Property(message => message.QueuePosition);

        builder.HasIndex(message => new { message.Status, message.NextAttemptAt });
        builder.HasIndex(message => new { message.Status, message.ProcessingExpiresAt });
        builder.HasIndex(message => new { message.Purpose, message.CorrelationKey, message.Status });

        // The body purge's work list: sent messages that still have their bodies. Purged rows leave the index, so the
        // purge never rescans years of already-purged mail. Its query repeats this predicate word for word.
        builder.HasIndex(message => message.SentAt)
            .HasDatabaseName(SentWithBodiesIndexName)
            .HasFilter(SentWithBodiesIndexFilter);

        // The outbox's two claim queries (EmailOutboxDispatcher) and the queue-position lookups (EmailOutboxService).
        // Each query repeats its index's filter word for word, so Postgres can use the partial index.
        builder.HasIndex(message => new { message.QueuePosition, message.Id })
            .HasDatabaseName(CampaignQueueIndexName)
            .HasFilter(CampaignQueueFilter);
        builder.HasIndex(message => new { message.CreatorId, message.QueuePosition })
            .HasDatabaseName(CreatorCampaignQueueIndexName)
            .HasFilter(CampaignQueueFilter);
        builder.HasIndex(message => new { message.CreatedAt, message.Id })
            .HasDatabaseName(TransactionalQueueIndexName)
            .HasFilter(TransactionalQueueFilter);
    }
}
