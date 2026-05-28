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

        builder.Property(plan => plan.PublicId)
            .IsRequired();

        builder.HasIndex(plan => plan.PublicId)
            .IsUnique();

        builder.Property(plan => plan.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(plan => plan.Code)
            .IsUnique();

        builder.Property(plan => plan.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(plan => plan.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(plan => plan.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(plan => plan.MonthlyPriceCents)
            .IsRequired();

        builder.Property(plan => plan.YearlyPriceCents)
            .IsRequired();

        builder.Property(plan => plan.MaxContacts)
            .IsRequired();

        builder.Property(plan => plan.MaxLandingPages)
            .IsRequired();

        builder.Property(plan => plan.MaxProducts)
            .IsRequired();

        builder.Property(plan => plan.MaxTeamMembers)
            .IsRequired();

        builder.Property(plan => plan.MaxEmailSendsPerMonth)
            .IsRequired();

        builder.Property(plan => plan.PlatformFeeBasisPoints)
            .IsRequired();

        builder.Property(plan => plan.FeaturesJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(plan => plan.IsActive)
            .IsRequired();

        builder.Property(plan => plan.SortOrder)
            .IsRequired();

        builder.Property(plan => plan.CreatedAt)
            .IsRequired();

        builder.Property(plan => plan.UpdatedAt)
            .IsRequired();

        builder.HasIndex(plan => new { plan.IsActive, plan.SortOrder });

        builder.HasData(
            new
            {
                Id = 1,
                PublicId = Guid.Parse("2b4d0d95-884d-4f73-8d1f-c21f6a87d901"),
                Code = "free",
                Name = "Free",
                Description = "Start selling with basic creator tools.",
                Currency = Currency.Eur,
                MonthlyPriceCents = 0,
                YearlyPriceCents = 0,
                MaxContacts = 500,
                MaxLandingPages = 1,
                MaxProducts = 1,
                MaxTeamMembers = 1,
                MaxEmailSendsPerMonth = 500,
                PlatformFeeBasisPoints = 1000,
                FeaturesJson = """
                               [
                                 "Basic creator workspace",
                                 "One landing page",
                                 "One product",
                                 "Email support"
                               ]
                               """,
                IsActive = true,
                SortOrder = 1,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new
            {
                Id = 2,
                PublicId = Guid.Parse("91cbbbf5-6c3d-4e1e-8f24-65bbd47f7296"),
                Code = "basic",
                Name = "Basic",
                Description = "For creators growing beyond the free tier.",
                Currency = Currency.Eur,
                MonthlyPriceCents = 1500,
                YearlyPriceCents = 15000,
                MaxContacts = 1500,
                MaxLandingPages = 5,
                MaxProducts = 5,
                MaxTeamMembers = 2,
                MaxEmailSendsPerMonth = 1500,
                PlatformFeeBasisPoints = 500,
                FeaturesJson = """
                               [
                                 "Everything in Free",
                                 "More landing pages",
                                 "More products",
                                 "Lower platform limits"
                               ]
                               """,
                IsActive = true,
                SortOrder = 2,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new
            {
                Id = 3,
                PublicId = Guid.Parse("fac3d73e-6cfd-41f7-81ff-fd8f2c31e2e7"),
                Code = "pro",
                Name = "Pro",
                Description = "For creators selling consistently.",
                Currency = Currency.Eur,
                MonthlyPriceCents = 3000,
                YearlyPriceCents = 30000,
                MaxContacts = 5000,
                MaxLandingPages = 20,
                MaxProducts = 25,
                MaxTeamMembers = 5,
                MaxEmailSendsPerMonth = 5000,
                PlatformFeeBasisPoints = 250,
                FeaturesJson = """
                               [
                                 "Everything in Basic",
                                 "Advanced analytics",
                                 "More team seats",
                                 "Priority support"
                               ]
                               """,
                IsActive = true,
                SortOrder = 3,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            },
            new
            {
                Id = 4,
                PublicId = Guid.Parse("74414886-cae9-4e6e-91c6-07b690409f36"),
                Code = "plus",
                Name = "Plus",
                Description = "For high-volume creators and small teams.",
                Currency = Currency.Eur,
                MonthlyPriceCents = 9900,
                YearlyPriceCents = 99000,
                MaxContacts = 20000,
                MaxLandingPages = 100,
                MaxProducts = 100,
                MaxTeamMembers = 15,
                MaxEmailSendsPerMonth = 20000,
                PlatformFeeBasisPoints = 100,
                FeaturesJson = """
                               [
                                 "Everything in Pro",
                                 "High-volume limits",
                                 "Team workspace",
                                 "Premium support"
                               ]
                               """,
                IsActive = true,
                SortOrder = 4,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            });
    }
}
