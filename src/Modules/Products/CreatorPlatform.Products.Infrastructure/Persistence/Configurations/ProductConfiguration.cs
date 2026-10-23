using CreatorPlatform.Creators.Domain.Creators;
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

        builder.Property(product => product.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(product => product.ThumbnailUrl)
            .HasMaxLength(2000);

        builder.Property(product => product.FileBlobPath)
            .HasMaxLength(ProductFile.MaxBlobPathLength);

        builder.Property(product => product.FileName)
            .HasMaxLength(ProductFile.MaxFileNameLength);

        builder.Property(product => product.FileSizeBytes);

        builder.Property(product => product.FileContentType)
            .HasMaxLength(ProductFile.MaxContentTypeLength);

        builder.Property(product => product.FileUploadedAt);

        builder.Property(product => product.CreatedAt)
            .IsRequired();

        builder.Property(product => product.UpdatedAt)
            .IsRequired();

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(product => product.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("CK_products_PriceCents_NonNegative", "\"PriceCents\" >= 0"));

        // The file's columns are set or cleared together, and an Active product always has a file.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_products_File_Complete",
            "(\"FileBlobPath\" IS NULL AND \"FileName\" IS NULL AND \"FileSizeBytes\" IS NULL AND \"FileContentType\" IS NULL AND \"FileUploadedAt\" IS NULL) " +
            "OR (\"FileBlobPath\" IS NOT NULL AND \"FileName\" IS NOT NULL AND \"FileSizeBytes\" > 0 AND \"FileContentType\" IS NOT NULL AND \"FileUploadedAt\" IS NOT NULL)"));
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_products_Active_Has_File",
            "\"Status\" <> 'Active' OR \"FileBlobPath\" IS NOT NULL"));
    }
}
