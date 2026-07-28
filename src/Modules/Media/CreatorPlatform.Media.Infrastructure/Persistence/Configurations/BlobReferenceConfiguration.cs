using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Media.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Media.Infrastructure.Persistence.Configurations;

public sealed class BlobReferenceConfiguration : IEntityTypeConfiguration<BlobReference>
{
    public void Configure(EntityTypeBuilder<BlobReference> builder)
    {
        builder.ToTable("blob_references", "media");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .ValueGeneratedOnAdd();

        builder.Property(b => b.PublicId)
            .IsRequired();

        builder.HasIndex(b => b.PublicId)
            .IsUnique();

        builder.Property(b => b.CreatorId)
            .IsRequired();

        builder.Property(b => b.Purpose)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.BlobUrl)
            .HasMaxLength(2000)
            .IsRequired();

        builder.HasIndex(b => b.BlobUrl)
            .IsUnique();

        builder.Property(b => b.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.SizeBytes);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.ConfirmedAt);

        builder.HasIndex(b => new { b.CreatorId, b.CreatedAt });

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(b => b.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
