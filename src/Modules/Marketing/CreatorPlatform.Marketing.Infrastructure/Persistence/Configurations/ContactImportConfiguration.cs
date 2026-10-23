using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Marketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Marketing.Infrastructure.Persistence.Configurations;

public sealed class ContactImportConfiguration : IEntityTypeConfiguration<ContactImport>
{
    public void Configure(EntityTypeBuilder<ContactImport> builder)
    {
        builder.ToTable("contact_imports", "marketing");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd();

        builder.Property(i => i.PublicId)
            .IsRequired();

        builder.HasIndex(i => i.PublicId)
            .IsUnique();

        builder.Property(i => i.CreatorId)
            .IsRequired();

        // The attesting user, kept as a plain id (no FK into auth.users): the consent record must survive whatever
        // later happens to that account.
        builder.Property(i => i.UserId)
            .IsRequired();

        builder.Property(i => i.FileName)
            .HasMaxLength(ContactImport.MaxFileNameLength)
            .IsRequired();

        builder.Property(i => i.RowCount).IsRequired();
        builder.Property(i => i.ImportedCount).IsRequired();
        builder.Property(i => i.ExistingCount).IsRequired();
        builder.Property(i => i.DuplicateCount).IsRequired();
        builder.Property(i => i.UnsubscribedCount).IsRequired();
        builder.Property(i => i.InvalidCount).IsRequired();

        builder.Property(i => i.ConsentVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.ConsentAttestedAt)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.HasIndex(i => new { i.CreatorId, i.CreatedAt });

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(i => i.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
