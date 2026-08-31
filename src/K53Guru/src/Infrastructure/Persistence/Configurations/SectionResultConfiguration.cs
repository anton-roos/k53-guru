using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class SectionResultConfiguration : IEntityTypeConfiguration<SectionResult>
{
    public void Configure(EntityTypeBuilder<SectionResult> builder)
    {
        builder.Property(sr => sr.Section).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.Ignore(e => e.DomainEvents);
    }
}
