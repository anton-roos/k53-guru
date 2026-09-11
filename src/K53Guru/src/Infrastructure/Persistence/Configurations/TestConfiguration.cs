using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class TestConfiguration : IEntityTypeConfiguration<Test>
{
    public void Configure(EntityTypeBuilder<Test> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Codes).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(t => t.Sections).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Ignore(e => e.DomainEvents);
    }
}
