using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payouts.Domain.Payouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Payouts.Infrastructure.Persistence.Configurations;

public sealed class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("payouts", "payouts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.PublicId)
            .IsRequired();

        builder.HasIndex(p => p.PublicId)
            .IsUnique();

        builder.Property(p => p.CreatorId)
            .IsRequired();

        builder.Property(p => p.AmountCents)
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.BankReference)
            .HasMaxLength(255);

        builder.Property(p => p.Note)
            .HasMaxLength(500);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.PaidAt);

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.HasIndex(p => new { p.CreatorId, p.CreatedAt });

        builder.HasIndex(p => p.Status);

        // DB-level backstop for "max one active payout request per creator" — the app also checks this
        // under the creator's advisory lock before inserting, but a unique index survives even if that
        // check is ever bypassed by a bug or a second app instance.
        builder.HasIndex(p => p.CreatorId)
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");

        builder.ToTable(t => t.HasCheckConstraint("CK_payouts_AmountCents_Positive", "\"AmountCents\" > 0"));

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(p => p.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
