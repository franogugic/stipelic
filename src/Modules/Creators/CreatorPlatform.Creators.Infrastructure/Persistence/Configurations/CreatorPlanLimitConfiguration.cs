using CreatorPlatform.Creators.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Creators.Infrastructure.Persistence.Configurations;

public sealed class CreatorPlanLimitConfiguration : IEntityTypeConfiguration<CreatorPlanLimit>
{
    private static readonly DateTimeOffset SeededAt = new(2026, 5, 26, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<CreatorPlanLimit> builder)
    {
        builder.ToTable("creator_plan_limits", "creators");

        builder.HasKey(limit => limit.Id);

        builder.Property(limit => limit.Id)
            .ValueGeneratedOnAdd();

        builder.Property(limit => limit.PlanId)
            .IsRequired();

        builder.Property(limit => limit.LimitKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(limit => limit.LimitValue)
            .IsRequired();

        builder.Property(limit => limit.CreatedAt)
            .IsRequired();

        builder.Property(limit => limit.UpdatedAt)
            .IsRequired();

        builder.HasIndex(limit => new { limit.PlanId, limit.LimitKey })
            .IsUnique();

        builder.HasData(
            Limit(1, 1, "max_landing_pages", 1),
            Limit(2, 1, "max_products", 1),
            Limit(3, 1, "max_members", 1),
            Limit(4, 1, "max_email_sends_per_month", 500),
            Limit(5, 1, "max_contacts", 500),
            Limit(6, 2, "max_landing_pages", 5),
            Limit(7, 2, "max_products", 5),
            Limit(8, 2, "max_members", 2),
            Limit(9, 2, "max_email_sends_per_month", 1500),
            Limit(10, 2, "max_contacts", 1500),
            Limit(11, 3, "max_landing_pages", 20),
            Limit(12, 3, "max_products", 25),
            Limit(13, 3, "max_members", 5),
            Limit(14, 3, "max_email_sends_per_month", 5000),
            Limit(15, 3, "max_contacts", 5000),
            Limit(16, 4, "max_landing_pages", 100),
            Limit(17, 4, "max_products", 100),
            Limit(18, 4, "max_members", 15),
            Limit(19, 4, "max_email_sends_per_month", -1),
            Limit(20, 4, "max_contacts", -1));
    }

    private static object Limit(int id, int planId, string key, int value)
    {
        return new
        {
            Id = id,
            PlanId = planId,
            LimitKey = key,
            LimitValue = value,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };
    }
}
