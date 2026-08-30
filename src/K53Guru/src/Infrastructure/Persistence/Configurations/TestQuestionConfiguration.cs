using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class TestQuestionConfiguration : IEntityTypeConfiguration<TestQuestion>
{
    public void Configure(EntityTypeBuilder<TestQuestion> builder)
    {
        builder.HasOne(t => t.Question)
            .WithMany()
            .HasForeignKey(t => t.QuestionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // A given Question may only appear once per Test - no duplicate membership rows.
        builder.HasIndex(t => new { t.TestId, t.QuestionId }).IsUnique();

        builder.Ignore(e => e.DomainEvents);
    }
}
