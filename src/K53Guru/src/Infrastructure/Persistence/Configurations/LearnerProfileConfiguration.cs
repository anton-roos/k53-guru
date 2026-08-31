using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K53Guru.Infrastructure.Persistence.Configurations;

#nullable disable
public class LearnerProfileConfiguration : IEntityTypeConfiguration<LearnerProfile>
{
    public void Configure(EntityTypeBuilder<LearnerProfile> builder)
    {
        builder.HasKey(x => x.Id);

        // Client-supplied UUID - the client generates its own id locally with no server "register"
        // step, so EF must never try to generate one on insert.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.HasMany(x => x.Attempts)
            .WithOne()
            .HasForeignKey(a => a.LearnerProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
