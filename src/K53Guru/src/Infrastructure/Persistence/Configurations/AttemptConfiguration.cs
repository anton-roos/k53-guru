using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.Property(a => a.Code).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.HasMany(a => a.AttemptQuestions)
            .WithOne()
            .HasForeignKey(aq => aq.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(e => e.DomainEvents);
    }
}
