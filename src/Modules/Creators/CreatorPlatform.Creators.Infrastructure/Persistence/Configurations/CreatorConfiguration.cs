using CreatorPlatform.Auth.Domain.Users;
using CreatorPlatform.Creators.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Creators.Infrastructure.Persistence.Configurations;

public sealed class CreatorConfiguration : IEntityTypeConfiguration<Creator>
{
    public void Configure(EntityTypeBuilder<Creator> builder)
    {
        builder.ToTable("creators", "creators");

        builder.HasKey(creator => creator.Id);

        builder.Property(creator => creator.Id)
            .ValueGeneratedOnAdd();

        builder.Property(creator => creator.PublicId)
            .IsRequired();

        builder.HasIndex(creator => creator.PublicId)
            .IsUnique();

        builder.Property(creator => creator.OwnerUserId)
            .IsRequired();

        builder.HasIndex(creator => creator.OwnerUserId)
            .IsUnique()
            .HasFilter("\"Status\" <> 'Disabled'");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(creator => creator.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(creator => creator.Name)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(creator => creator.Slug)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(creator => creator.Slug)
            .IsUnique()
            .HasFilter("\"Status\" <> 'Disabled'");

        builder.Property(creator => creator.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(creator => creator.DefaultCurrency)
            .HasConversion<string>()
            .HasMaxLength(5)
            .IsRequired();

        builder.Property(creator => creator.StripeCustomerId)
            .HasMaxLength(100);

        builder.HasIndex(creator => creator.StripeCustomerId)
            .IsUnique()
            .HasFilter("\"StripeCustomerId\" IS NOT NULL");

        builder.Property(creator => creator.CreatedAt)
            .IsRequired();

        builder.Property(creator => creator.UpdatedAt)
            .IsRequired();
    }
}
