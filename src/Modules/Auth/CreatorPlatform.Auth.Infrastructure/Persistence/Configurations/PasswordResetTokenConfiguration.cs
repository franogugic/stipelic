using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Auth.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Auth.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens", "auth");

        builder.HasKey(token => token.Id);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(token => token.TokenHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        builder.Property(token => token.ExpiresAt)
            .IsRequired();

        builder.Property(token => token.CreatedAt)
            .IsRequired();
    }
}
