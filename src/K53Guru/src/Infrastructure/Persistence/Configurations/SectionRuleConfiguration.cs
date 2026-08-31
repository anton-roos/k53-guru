using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class SectionRuleConfiguration : IEntityTypeConfiguration<SectionRule>
{
    public void Configure(EntityTypeBuilder<SectionRule> builder)
    {
        builder.Property(t => t.Section).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.Ignore(e => e.DomainEvents);
    }
}
