using CreatorPlatform.Auth.Domain.Users;
using CreatorPlatform.Creators.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Creators.Infrastructure.Persistence.Configurations;

public sealed class CreatorMemberConfiguration : IEntityTypeConfiguration<CreatorMember>
{
    public void Configure(EntityTypeBuilder<CreatorMember> builder)
    {
        builder.ToTable("creator_members", "creators");

        builder.HasKey(member => member.Id);

        builder.Property(member => member.Id)
            .ValueGeneratedOnAdd();

        builder.Property(member => member.PublicId)
            .IsRequired();

        builder.HasIndex(member => member.PublicId)
            .IsUnique();

        builder.Property(member => member.CreatorId)
            .IsRequired();

        builder.HasOne(member => member.Creator)
            .WithMany()
            .HasForeignKey(member => member.CreatorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(member => member.UserId)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(member => member.Role)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(member => member.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(member => member.CreatedAt)
            .IsRequired();

        builder.HasIndex(member => new { member.CreatorId, member.UserId })
            .IsUnique();

        builder.HasIndex(member => new { member.UserId, member.Status });
    }
}
