using CreatorPlatform.Creators.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Creators.Infrastructure.Persistence.Configurations;

public sealed class CreatorSubscriptionConfiguration : IEntityTypeConfiguration<CreatorSubscription>
{
    public void Configure(EntityTypeBuilder<CreatorSubscription> builder)
    {
        builder.ToTable("creator_subscriptions", "creators");

        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.Id)
            .ValueGeneratedOnAdd();

        builder.Property(subscription => subscription.PublicId)
            .IsRequired();

        builder.HasIndex(subscription => subscription.PublicId)
            .IsUnique();

        builder.Property(subscription => subscription.CreatorId)
            .IsRequired();

        builder.HasOne(subscription => subscription.Creator)
            .WithMany()
            .HasForeignKey(subscription => subscription.CreatorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(subscription => subscription.PlanId)
            .IsRequired();

        builder.HasOne(subscription => subscription.Plan)
            .WithMany()
            .HasForeignKey(subscription => subscription.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(subscription => subscription.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(subscription => subscription.BillingInterval)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(subscription => subscription.Provider)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(subscription => subscription.ProviderSubscriptionId)
            .HasMaxLength(255);

        builder.Property(subscription => subscription.CreatedAt)
            .IsRequired();

        builder.Property(subscription => subscription.UpdatedAt)
            .IsRequired();

        builder.HasIndex(subscription => new { subscription.CreatorId, subscription.Status });

        builder.HasIndex(subscription => subscription.ProviderSubscriptionId)
            .IsUnique();
    }
}
