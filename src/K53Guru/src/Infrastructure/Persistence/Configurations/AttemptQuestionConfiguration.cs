using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class AttemptQuestionConfiguration : IEntityTypeConfiguration<AttemptQuestion>
{
    public void Configure(EntityTypeBuilder<AttemptQuestion> builder)
    {
        builder.Property(q => q.Section).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(q => q.Code).HasConversion<string>().HasMaxLength(50).IsRequired();

        // Mirrors QuestionConfiguration's Stem/SignRef lengths - these are verbatim copies of the
        // source Question's content at snapshot time.
        builder.Property(q => q.Stem).HasMaxLength(2000).IsRequired();
        builder.Property(q => q.SignRef).HasMaxLength(20);

        builder.HasMany(q => q.AttemptAnswerOptions)
            .WithOne()
            .HasForeignKey(o => o.AttemptQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(e => e.DomainEvents);
    }
}
