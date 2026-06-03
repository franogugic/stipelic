using CreatorPlatform.Products.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Products.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "products");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .ValueGeneratedOnAdd();

        builder.Property(product => product.PublicId)
            .IsRequired();

        builder.HasIndex(product => product.PublicId)
            .IsUnique();

        builder.Property(product => product.CreatorId)
            .IsRequired();

        builder.HasIndex(product => new { product.CreatorId, product.Status });

        builder.HasIndex(product => new { product.CreatorId, product.CreatedAt });

        builder.Property(product => product.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(2000);

        builder.Property(product => product.PriceCents)
            .IsRequired();

        builder.Property(product => product.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(product => product.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(product => product.AccessUrl)
            .HasMaxLength(2000);

        builder.Property(product => product.ThumbnailUrl)
            .HasMaxLength(2000);

        builder.Property(product => product.CreatedAt)
            .IsRequired();

        builder.Property(product => product.UpdatedAt)
            .IsRequired();
    }
}
