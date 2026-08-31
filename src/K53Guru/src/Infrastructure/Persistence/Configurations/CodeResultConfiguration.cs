using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class CodeResultConfiguration : IEntityTypeConfiguration<CodeResult>
{
    public void Configure(EntityTypeBuilder<CodeResult> builder)
    {
        builder.Property(cr => cr.Code).HasConversion<string>().HasMaxLength(50).IsRequired();

        // No navigation on Attempt back to its CodeResults (unlike AttemptQuestions) - grading
        // writes CodeResult rows directly, keyed by AttemptId, without needing Attempt to expose
        // them as a collection.
        builder.HasOne<Attempt>()
            .WithMany()
            .HasForeignKey(cr => cr.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(cr => cr.SectionResults)
            .WithOne()
            .HasForeignKey(sr => sr.CodeResultId)
            .OnDelete(DeleteBehavior.Cascade);

        // Guards against the concurrent double-submit race: two concurrent SubmitAttemptCommand
        // calls for the same Attempt can both observe SubmittedAt == null and both attempt to
        // persist a full CodeResult set. This unique index turns the loser's write into a real,
        // catchable unique-constraint violation on SaveChangesAsync instead of silently succeeding
        // with duplicate grading results.
        builder.HasIndex(cr => new { cr.AttemptId, cr.Code }).IsUnique();

        builder.Ignore(e => e.DomainEvents);
    }
}
