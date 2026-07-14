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

        // Idempotency guard: a given order can have at most one entry of a given type — protects against
        // double-booking the same webhook event on retry.
        builder.HasIndex(e => new { e.OrderId, e.Type })
            .IsUnique()
            .HasFilter("\"OrderId\" IS NOT NULL");

        builder.HasIndex(e => e.PayoutId)
            .HasFilter("\"PayoutId\" IS NOT NULL");

        builder.ToTable(t => t.HasCheckConstraint("CK_ledger_entries_AmountCents_NotZero", "\"AmountCents\" <> 0"));

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
