using CreatorPlatform.Creators.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Creators.Infrastructure.Persistence.Configurations;

public sealed class CreatorPayoutProfileConfiguration : IEntityTypeConfiguration<CreatorPayoutProfile>
{
    public void Configure(EntityTypeBuilder<CreatorPayoutProfile> builder)
    {
        builder.ToTable("creator_payout_profiles", "creators");

        builder.HasKey(profile => profile.CreatorId);

        builder.HasOne(profile => profile.Creator)
            .WithOne()
            .HasForeignKey<CreatorPayoutProfile>(profile => profile.CreatorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(profile => profile.AccountHolderName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(profile => profile.Iban)
            .HasMaxLength(34)
            .IsRequired();

        builder.Property(profile => profile.BankCountryCode)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(profile => profile.CreatedAt)
            .IsRequired();

        builder.Property(profile => profile.UpdatedAt)
            .IsRequired();
    }
}
