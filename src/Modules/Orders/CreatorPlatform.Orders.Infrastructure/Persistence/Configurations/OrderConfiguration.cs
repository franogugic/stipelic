using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Products.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .ValueGeneratedOnAdd();

        builder.Property(o => o.PublicId)
            .IsRequired();

        builder.HasIndex(o => o.PublicId)
            .IsUnique();

        builder.Property(o => o.CreatorId)
            .IsRequired();

        builder.Property(o => o.ProductId)
            .IsRequired();

        builder.Property(o => o.LandingPageId);

        builder.Property(o => o.Email)
            .HasMaxLength(254);

        builder.Property(o => o.Name)
            .HasMaxLength(Order.NameMaxLength);

        builder.Property(o => o.BuyerCountryCode)
            .HasMaxLength(2);

        builder.Property(o => o.AmountCents)
            .IsRequired();

        builder.Property(o => o.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.PlatformFeeBasisPoints)
            .IsRequired();

        builder.Property(o => o.PlatformFeeCents)
            .IsRequired();

        // Nullable: the Pending order is saved before its Checkout session exists (the session's metadata carries
        // the order's PublicId). Unique still holds — PostgreSQL treats NULLs as distinct.
        builder.Property(o => o.StripeCheckoutSessionId)
            .HasMaxLength(255);

        builder.HasIndex(o => o.StripeCheckoutSessionId)
            .IsUnique();

        builder.Property(o => o.StripePaymentIntentId)
            .HasMaxLength(255);

        builder.Property(o => o.RefundedCents)
            .IsRequired();

        // Revenue totals sum what a Paid order keeps after a partial refund. Stored generated columns, so every query
        // (SQL or LINQ) reads one rule; the fee's share is rounded half away from zero, as PayoutLedgerService does.
        builder.Property(o => o.KeptAmountCents)
            .HasComputedColumnSql("\"AmountCents\" - \"RefundedCents\"", stored: true);

        builder.Property(o => o.KeptPlatformFeeCents)
            .HasComputedColumnSql(
                "\"PlatformFeeCents\" - CASE WHEN \"AmountCents\" > 0 " +
                "THEN round(\"PlatformFeeCents\"::numeric * \"RefundedCents\" / \"AmountCents\")::integer ELSE 0 END",
                stored: true);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.PaidAt);

        builder.Property(o => o.UpdatedAt)
            .IsRequired();

        builder.HasIndex(o => new { o.CreatorId, o.CreatedAt });

        // Support the Orders page's product/status filters staying index-backed even when combined
        // with the keyset CreatedAt ordering, instead of falling back to a filtered scan of the
        // (CreatorId, CreatedAt) index once a creator has enough order history to matter.
        builder.HasIndex(o => new { o.CreatorId, o.Status, o.CreatedAt });

        builder.HasIndex(o => new { o.CreatorId, o.ProductId, o.CreatedAt });

        builder.HasIndex(o => new { o.CreatorId, o.LandingPageId, o.CreatedAt });

        builder.HasIndex(o => o.LandingPageId);

        builder.HasIndex(o => o.Email);

        builder.ToTable(t => t.HasCheckConstraint("CK_orders_AmountCents_NonNegative", "\"AmountCents\" >= 0"));
        builder.ToTable(t => t.HasCheckConstraint("CK_orders_PlatformFeeCents_NonNegative", "\"PlatformFeeCents\" >= 0"));
        // Only an order whose session was never stored can lack one: a paid or refunded order always has its session.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_orders_Session_Unless_PendingOrFailed",
            "\"StripeCheckoutSessionId\" IS NOT NULL OR \"Status\" IN ('Pending', 'Failed')"));
        // A full refund refunds the whole amount; only a paid (or refunded) order can carry a refund. Refund figures sum
        // RefundedCents, so this keeps them exact.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_orders_RefundedCents_MatchStatus",
            "(\"Status\" <> 'Refunded' OR \"RefundedCents\" = \"AmountCents\") " +
            "AND (\"RefundedCents\" = 0 OR \"Status\" IN ('Paid', 'Refunded'))"));
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_orders_RefundedCents_WithinAmount", "\"RefundedCents\" >= 0 AND \"RefundedCents\" <= \"AmountCents\""));

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(o => o.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(o => o.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<LandingPage>()
            .WithMany()
            .HasForeignKey(o => o.LandingPageId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
