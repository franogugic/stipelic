using CreatorPlatform.Creators.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Creators.Infrastructure.Persistence.Configurations;

public sealed class CreatorPlanConfiguration : IEntityTypeConfiguration<CreatorPlan>
{
    private static readonly DateTimeOffset SeededAt = new(2026, 5, 26, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<CreatorPlan> builder)
    {
        builder.ToTable("creator_plans", "creators");

        builder.HasKey(plan => plan.Id);

        builder.Property(plan => plan.Id)
            .ValueGeneratedOnAdd();

        builder.Property(plan => plan.Code)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(plan => plan.Code)
            .IsUnique();

        builder.Property(plan => plan.Name)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(plan => plan.Description)
            .HasMaxLength(500);

        builder.Property(plan => plan.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(plan => plan.PriceCents)
            .IsRequired();

        builder.Property(plan => plan.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(plan => plan.BillingInterval)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(plan => plan.PlatformFeeBasisPoints)
            .IsRequired();

        builder.Property(plan => plan.CreatedAt)
            .IsRequired();

        builder.Property(plan => plan.UpdatedAt)
            .IsRequired();

        builder.HasIndex(plan => plan.Status);

        builder.HasMany(plan => plan.Limits)
            .WithOne(limit => limit.Plan)
            .HasForeignKey(limit => limit.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(plan => plan.Limits)
            .HasField("_limits")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(
            new
            {
                Id = 1,
                Code = "free",
                Name = "Free",
                Description = "For creators with up to 500 contacts.",
                Status = CreatorPlanStatus.Active,
                PriceCents = 0,
                Currency = Currency.Eur,
                BillingInterval = BillingInterval.None,
                PlatformFeeBasisPoints = 1000,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new
            {
                Id = 2,
                Code = "basic",
                Name = "Basic",
                Description = "For creators with up to 1,500 contacts.",
                Status = CreatorPlanStatus.Active,
                PriceCents = 1500,
                Currency = Currency.Eur,
                BillingInterval = BillingInterval.Monthly,
                PlatformFeeBasisPoints = 500,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new
            {
                Id = 3,
                Code = "pro",
                Name = "Pro",
                Description = "For creators with up to 5,000 contacts.",
                Status = CreatorPlanStatus.Active,
                PriceCents = 3000,
                Currency = Currency.Eur,
                BillingInterval = BillingInterval.Monthly,
                PlatformFeeBasisPoints = 250,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new
            {
                Id = 4,
                Code = "plus",
                Name = "Pro Plus",
                Description = "For creators above 5,000 contacts.",
                Status = CreatorPlanStatus.Active,
                PriceCents = 9900,
                Currency = Currency.Eur,
                BillingInterval = BillingInterval.Monthly,
                PlatformFeeBasisPoints = 100,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            });
    }
}
