using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Marketing.Infrastructure.Persistence.Configurations;

public sealed class UnsubscribeConfiguration : IEntityTypeConfiguration<Unsubscribe>
{
    public void Configure(EntityTypeBuilder<Unsubscribe> builder)
    {
        builder.ToTable("unsubscribes", "marketing");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedOnAdd();

        builder.Property(u => u.CreatorId)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(u => u.Source)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.UnsubscribedAt)
            .IsRequired();

        builder.HasIndex(u => new { u.CreatorId, u.Email })
            .IsUnique();

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(u => u.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
