using CreatorPlatform.Auth.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Auth.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", "auth");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id)
            .ValueGeneratedNever();

        builder.Property(role => role.Name)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(role => role.Name)
            .IsUnique();

        builder.HasData(
            Role.Create((short)RoleId.User, "user"),
            Role.Create((short)RoleId.PlatformAdmin, "platform_admin"),
            Role.Create((short)RoleId.CreatorOwner, "creator_owner"),
            Role.Create((short)RoleId.CreatorStaff, "creator_staff"));
    }
}
