using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class TestConfigConfiguration : IEntityTypeConfiguration<TestConfig>
{
    public void Configure(EntityTypeBuilder<TestConfig> builder)
    {
        builder.Property(t => t.Code).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.HasMany(t => t.SectionRules)
            .WithOne()
            .HasForeignKey(sr => sr.TestConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(e => e.DomainEvents);
    }
}
