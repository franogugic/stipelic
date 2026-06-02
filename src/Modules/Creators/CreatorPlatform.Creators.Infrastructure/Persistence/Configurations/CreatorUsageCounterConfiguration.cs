using CreatorPlatform.Creators.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Creators.Infrastructure.Persistence.Configurations;

public sealed class CreatorUsageCounterConfiguration : IEntityTypeConfiguration<CreatorUsageCounter>
{
    public void Configure(EntityTypeBuilder<CreatorUsageCounter> builder)
    {
        builder.ToTable("creator_usage_counters", "creators");

        builder.HasKey(counter => counter.Id);

        builder.Property(counter => counter.Id)
            .ValueGeneratedOnAdd();

        builder.Property(counter => counter.CreatorId)
            .IsRequired();

        builder.HasOne(counter => counter.Creator)
            .WithMany()
            .HasForeignKey(counter => counter.CreatorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(counter => counter.UsageKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(counter => counter.UsedValue)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(counter => counter.PeriodStart)
            .IsRequired();

        builder.Property(counter => counter.PeriodEnd)
            .IsRequired();

        builder.Property(counter => counter.CreatedAt)
            .IsRequired();

        builder.Property(counter => counter.UpdatedAt)
            .IsRequired();

        builder.HasIndex(counter => new
            {
                counter.CreatorId,
                counter.UsageKey,
                counter.PeriodStart,
                counter.PeriodEnd
            })
            .IsUnique();

        builder.HasIndex(counter => new { counter.CreatorId, counter.UsageKey });
    }
}
