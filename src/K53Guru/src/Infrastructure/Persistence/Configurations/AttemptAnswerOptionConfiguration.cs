using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class AttemptAnswerOptionConfiguration : IEntityTypeConfiguration<AttemptAnswerOption>
{
    public void Configure(EntityTypeBuilder<AttemptAnswerOption> builder)
    {
        // Mirrors AnswerOptionConfiguration's Text length - a verbatim copy of the source
        // AnswerOption's content at snapshot time.
        builder.Property(o => o.Text).HasMaxLength(1000).IsRequired();

        builder.Ignore(e => e.DomainEvents);
    }
}
