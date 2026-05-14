using CreatorPlatform.Auth.Domain.Sessions;
using CreatorPlatform.Auth.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Auth.Infrastructure.Persistence.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions", "auth");

        builder.HasKey(session => session.Id);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(session => session.SessionTokenHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(session => session.SessionTokenHash)
            .IsUnique();

        builder.Property(session => session.ExpiresAt)
            .IsRequired();

        builder.Property(session => session.CreatedAt)
            .IsRequired();

        builder.HasIndex(session => new { session.UserId, session.ExpiresAt });
    }
}
