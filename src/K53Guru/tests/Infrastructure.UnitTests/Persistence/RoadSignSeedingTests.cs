using System.Reflection;
using K53Guru.Domain.Identity;
using K53Guru.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Persistence;

/// <summary>
/// Covers spec-1-1-seed-road-sign-catalog.md I/O &amp; Edge-Case Matrix rows:
///   - "First-run seed": empty RoadSigns table -> ~20 signs inserted, each with a unique legislation_code.
///   - "Idempotent restart": RoadSigns already populated -> seeding is skipped, no duplicates created.
///
/// Uses a shared-connection SQLite in-memory database so the schema derived from
/// ApplicationDbContext's fluent configuration (including RoadSignConfiguration) is exercised,
/// and invokes the real, private ApplicationDbContextInitializer.SeedRoadSignsAsync method via
/// reflection so the production seeding code path is what's actually under test.
/// </summary>
public class RoadSignSeedingTests : IDisposable
{
    private const int ExpectedSeedCount = 20;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public RoadSignSeedingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task FirstRunSeed_EmptyRoadSignsTable_InsertsTwentyUniqueSigns()
    {
        // Arrange
        var initializer = CreateInitializer();

        // Act
        await InvokeSeedRoadSignsAsync(initializer);

        // Assert
        await using var verifyContext = new ApplicationDbContext(_options);
        var signs = await verifyContext.RoadSigns.ToListAsync();

        Assert.Equal(ExpectedSeedCount, signs.Count);
        Assert.All(signs, s => Assert.False(string.IsNullOrWhiteSpace(s.LegislationCode)));
        Assert.All(signs, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));

        var distinctCodes = signs.Select(s => s.LegislationCode).Distinct(StringComparer.Ordinal).Count();
        Assert.Equal(ExpectedSeedCount, distinctCodes);
    }

    [Fact]
    public async Task IdempotentRestart_RoadSignsAlreadyPopulated_SkipsSeedingWithoutDuplicates()
    {
        // Arrange: first run seeds the catalog.
        var firstRunInitializer = CreateInitializer();
        await InvokeSeedRoadSignsAsync(firstRunInitializer);

        // Act: simulate an application restart - a brand-new initializer instance, backed by
        // the same (already-populated) store, runs the seed step again.
        var restartInitializer = CreateInitializer();
        var exception = await Record.ExceptionAsync(() => InvokeSeedRoadSignsAsync(restartInitializer));

        // Assert
        Assert.Null(exception);

        await using var verifyContext = new ApplicationDbContext(_options);
        var signs = await verifyContext.RoadSigns.ToListAsync();

        Assert.Equal(ExpectedSeedCount, signs.Count);
        var distinctCodes = signs.Select(s => s.LegislationCode).Distinct(StringComparer.Ordinal).Count();
        Assert.Equal(ExpectedSeedCount, distinctCodes);
    }

    private ApplicationDbContextInitializer CreateInitializer()
    {
        var factoryMock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factoryMock.Setup(f => f.CreateDbContext()).Returns(() => new ApplicationDbContext(_options));

        var loggerMock = new Mock<ILogger<ApplicationDbContextInitializer>>();

        // SeedRoadSignsAsync never touches UserManager/RoleManager - these are only present to
        // satisfy the initializer's constructor and are never invoked in the code path under test.
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        var roleManagerMock = new Mock<RoleManager<ApplicationRole>>(
            Mock.Of<IRoleStore<ApplicationRole>>(), null, null, null, null);

        return new ApplicationDbContextInitializer(
            loggerMock.Object,
            factoryMock.Object,
            userManagerMock.Object,
            roleManagerMock.Object);
    }

    private static async Task InvokeSeedRoadSignsAsync(ApplicationDbContextInitializer initializer)
    {
        var method = typeof(ApplicationDbContextInitializer)
            .GetMethod("SeedRoadSignsAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        var task = (Task)method!.Invoke(initializer, null)!;
        await task;
    }
}
