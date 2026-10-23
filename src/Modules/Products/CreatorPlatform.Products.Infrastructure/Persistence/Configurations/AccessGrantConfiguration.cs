using CreatorPlatform.Analytics.Domain.EmailCaptures;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Products.Domain.Access;
using CreatorPlatform.Products.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Products.Infrastructure.Persistence.Configurations;

public sealed class AccessGrantConfiguration : IEntityTypeConfiguration<AccessGrant>
{
    public void Configure(EntityTypeBuilder<AccessGrant> builder)
    {
        builder.ToTable("access_grants", "products");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .ValueGeneratedOnAdd();

        builder.Property(g => g.Token)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(g => g.Token)
            .IsUnique();

        builder.Property(g => g.CreatorId).IsRequired();
        builder.Property(g => g.ProductId).IsRequired();
        builder.Property(g => g.OrderId);
        builder.Property(g => g.EmailCaptureId);

        builder.Property(g => g.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.RevokedAt);
        builder.Property(g => g.LastSentAt);

        builder.Property(g => g.DownloadCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(g => g.LastDownloadedAt);

        // One link per order; one per (sign-up, product) — a lead page that switches its document gives a returning
        // sign-up a new link for the new document.
        builder.HasIndex(g => g.OrderId)
            .IsUnique()
            .HasFilter("\"OrderId\" IS NOT NULL");

        builder.HasIndex(g => new { g.EmailCaptureId, g.ProductId })
            .IsUnique()
            .HasFilter("\"EmailCaptureId\" IS NOT NULL");

        builder.HasIndex(g => new { g.CreatorId, g.ProductId });

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_access_grants_One_Source",
            "(\"OrderId\" IS NOT NULL AND \"EmailCaptureId\" IS NULL) OR (\"OrderId\" IS NULL AND \"EmailCaptureId\" IS NOT NULL)"));

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(g => g.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(g => g.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(g => g.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // A deleted contact takes its captures with it; their links go too.
        builder.HasOne<EmailCapture>()
            .WithMany()
            .HasForeignKey(g => g.EmailCaptureId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
