using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Payouts.Domain.Payouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Payouts.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries", "payouts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.PublicId)
            .IsRequired();

        builder.HasIndex(e => e.PublicId)
            .IsUnique();

        builder.Property(e => e.CreatorId)
            .IsRequired();

        builder.Property(e => e.OrderId);

        builder.Property(e => e.PayoutId);

        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.AmountCents)
            .IsRequired();

        builder.Property(e => e.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(e => new { e.CreatorId, e.CreatedAt });

        builder.Property(e => e.RefundedToCents);

        // Idempotency guard: a given order has at most one sale credit and one fee debit, and at most one refund
        // entry of each type per cumulative refunded amount (a partial refund, then the rest) — protects against
        // double-booking the same webhook event on retry. NULLS NOT DISTINCT keeps the null-keyed sale entries unique.
        builder.HasIndex(e => new { e.OrderId, e.Type, e.RefundedToCents })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("\"OrderId\" IS NOT NULL");

        // Idempotency guard: a given payout can have at most one entry of a given type (PayoutDebit on
        // create, Adjustment on mark-failed) — protects against a racing duplicate admin request.
        builder.HasIndex(e => new { e.PayoutId, e.Type })
            .IsUnique()
            .HasFilter("\"PayoutId\" IS NOT NULL");

        builder.ToTable(t => t.HasCheckConstraint("CK_ledger_entries_AmountCents_NotZero", "\"AmountCents\" <> 0"));
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ledger_entries_RefundedToCents_OnRefundsOnly",
            "(\"Type\" IN ('RefundDebit', 'FeeRefundCredit')) = (\"RefundedToCents\" IS NOT NULL)"));

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(e => e.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<Payout>()
            .WithMany()
            .HasForeignKey(e => e.PayoutId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
