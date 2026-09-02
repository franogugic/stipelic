using CreatorPlatform.Analytics.Domain.PageViews;
using CreatorPlatform.Creators.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreatorPlatform.Analytics.Infrastructure.Persistence.Configurations;

public sealed class CreatorViewTotalConfiguration : IEntityTypeConfiguration<CreatorViewTotal>
{
    public void Configure(EntityTypeBuilder<CreatorViewTotal> builder)
    {
        builder.ToTable("creator_view_totals", "analytics");

        builder.HasKey(c => c.CreatorId);

        builder.Property(c => c.CreatorId)
            .ValueGeneratedNever();

        builder.Property(c => c.TotalViews)
            .IsRequired();

        builder.HasOne<Creator>()
            .WithMany()
            .HasForeignKey(c => c.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
