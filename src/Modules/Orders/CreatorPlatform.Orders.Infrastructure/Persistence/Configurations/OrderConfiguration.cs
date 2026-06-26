using CreatorPlatform.Orders.Domain.Orders;
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
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(o => o.Name)
            .HasMaxLength(50);

        builder.Property(o => o.AmountCents)
            .IsRequired();

        builder.Property(o => o.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.StripeCheckoutSessionId)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(o => o.StripeCheckoutSessionId)
            .IsUnique();

        builder.Property(o => o.StripePaymentIntentId)
            .HasMaxLength(255);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.PaidAt);

        builder.Property(o => o.UpdatedAt)
            .IsRequired();

        builder.HasIndex(o => new { o.CreatorId, o.CreatedAt });
    }
}
