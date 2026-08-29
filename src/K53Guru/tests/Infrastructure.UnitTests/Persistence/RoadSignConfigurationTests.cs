using K53Guru.Domain.Entities;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Persistence;

/// <summary>
/// Covers spec-1-1-seed-road-sign-catalog.md I/O &amp; Edge-Case Matrix row:
///   - "Duplicate in source": two seed entries share the same legislation_code -> seeding fails
///     with a clear exception (unique-index violation) before any data is committed.
///
/// Exercises the actual RoadSignConfiguration fluent mapping (unique index on LegislationCode)
/// against a real relational schema (SQLite in-memory), independent of the seed data itself,
/// since the shipped seed list is duplicate-free by construction.
/// </summary>
public class RoadSignConfigurationTests
{
    [Fact]
    public async Task SaveChanges_TwoRoadSignsWithSameLegislationCode_ThrowsAndCommitsNothing()
    {
        // Arrange
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var schemaContext = new ApplicationDbContext(options))
        {
            schemaContext.Database.EnsureCreated();
        }

        // Act
        await using (var context = new ApplicationDbContext(options))
        {
            context.RoadSigns.AddRange(
                new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1.png" },
                new RoadSign { LegislationCode = "R1", Description = "Duplicate stop", ImageAssetKey = "signs/r1-dup.png" });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        // Assert: the failed SaveChanges must not have committed either row.
        await using (var verifyContext = new ApplicationDbContext(options))
        {
            var count = await verifyContext.RoadSigns.CountAsync();
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task SaveChanges_TwoRoadSignsWithDifferentLegislationCodes_Succeeds()
    {
        // Arrange
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var schemaContext = new ApplicationDbContext(options))
        {
            schemaContext.Database.EnsureCreated();
        }

        // Act
        await using (var context = new ApplicationDbContext(options))
        {
            context.RoadSigns.AddRange(
                new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1.png" },
                new RoadSign { LegislationCode = "R2", Description = "Yield", ImageAssetKey = "signs/r2.png" });

            await context.SaveChangesAsync();
        }

        // Assert
        await using (var verifyContext = new ApplicationDbContext(options))
        {
            var count = await verifyContext.RoadSigns.CountAsync();
            Assert.Equal(2, count);
        }
    }
}
