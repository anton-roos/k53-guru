using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class RoadSignConfiguration : IEntityTypeConfiguration<RoadSign>
{
    public void Configure(EntityTypeBuilder<RoadSign> builder)
    {
        builder.Property(t => t.LegislationCode).HasMaxLength(20).IsRequired();
        builder.HasIndex(t => t.LegislationCode).IsUnique();
        builder.Property(t => t.Description).HasMaxLength(500).IsRequired();
        builder.Property(t => t.ImageAssetKey).HasMaxLength(500);
        builder.Ignore(e => e.DomainEvents);
    }
}
