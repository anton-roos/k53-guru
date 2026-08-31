using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(t => t.Stem).HasMaxLength(2000).IsRequired();
        builder.Property(t => t.Codes).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(t => t.Section).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(t => t.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(t => t.SignRef).HasMaxLength(20);
        builder.Property(t => t.Explanation).HasMaxLength(2000);

        builder.HasMany(t => t.AnswerOptions)
            .WithOne()
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(e => e.DomainEvents);
    }
}
