using CreatorPlatform.Creators.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Creators.Infrastructure.Persistence.Configurations;

public sealed class CreatorSettingsConfiguration : IEntityTypeConfiguration<CreatorSettings>
{
    public void Configure(EntityTypeBuilder<CreatorSettings> builder)
    {
        builder.ToTable("creator_settings", "creators");

        builder.HasKey(settings => settings.CreatorId);

        builder.HasOne(settings => settings.Creator)
            .WithOne()
            .HasForeignKey<CreatorSettings>(settings => settings.CreatorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(settings => settings.SupportEmail)
            .HasMaxLength(100);

        builder.Property(settings => settings.BrandName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(settings => settings.LogoUrl)
            .HasMaxLength(500);

        builder.Property(settings => settings.PrimaryColor)
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(settings => settings.Timezone)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(settings => settings.CreatedAt)
            .IsRequired();

        builder.Property(settings => settings.UpdatedAt)
            .IsRequired();
    }
}
