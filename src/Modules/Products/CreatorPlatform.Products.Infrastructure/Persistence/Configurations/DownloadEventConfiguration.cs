using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Products.Domain.Access;
using CreatorPlatform.Products.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Products.Infrastructure.Persistence.Configurations;

public sealed class DownloadEventConfiguration : IEntityTypeConfiguration<DownloadEvent>
{
    public void Configure(EntityTypeBuilder<DownloadEvent> builder)
    {
        builder.ToTable("download_events", "products");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityByDefaultColumn();
        builder.Property(e => e.DownloadedAt).IsRequired();

        // The analytics reads: per workspace, per product and per page, each over a time range.
        builder.HasIndex(e => new { e.CreatorId, e.DownloadedAt });
        builder.HasIndex(e => new { e.ProductId, e.DownloadedAt });
        builder.HasIndex(e => new { e.LandingPageId, e.DownloadedAt })
            .HasFilter("\"LandingPageId\" IS NOT NULL");
        builder.HasIndex(e => e.GrantId);

        // A link deleted with its sign-up takes its downloads with it.
        builder.HasOne<AccessGrant>()
            .WithMany()
            .HasForeignKey(e => e.GrantId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(e => e.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<LandingPage>()
            .WithMany()
            .HasForeignKey(e => e.LandingPageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
