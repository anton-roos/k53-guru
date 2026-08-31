using System.Reflection;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
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
/// Covers spec-3-2-configure-test-parameters.md I/O &amp; Edge-Case Matrix rows:
///   - "First run, no TestConfig rows exist": seeding creates exactly 3 TestConfig rows
///     (Code1/Code2/Code3), each with 3 SectionRules.
///   - "Second run, TestConfig rows already exist": seeding is idempotent (no-op, no
///     duplicate rows).
///   - "Seeded values are correct": each TestConfig.TimeLimitMinutes == 60; each code's
///     SectionRules are exactly Rules(30,22), Signs(30,23), VehicleControls(12,10).
///   - "Config value changed after seeding": a directly-updated SectionRule.PassMark is
///     reflected on a subsequent read - no hardcoding, no code change needed.
///
/// Uses a shared-connection SQLite in-memory database so the schema derived from
/// ApplicationDbContext's fluent configuration (including TestConfigConfiguration and
/// SectionRuleConfiguration) is exercised, and invokes the real, private
/// ApplicationDbContextInitializer.SeedTestConfigsAsync method via reflection so the
/// production seeding code path is what's actually under test - mirroring
/// RoadSignSeedingTests.cs's established harness.
/// </summary>
public class TestConfigSeedingTests : IDisposable
{
    private const int ExpectedConfigCount = 3;
    private const int ExpectedSectionRulesPerConfig = 3;
    private const int ExpectedTimeLimitMinutes = 60;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TestConfigSeedingTests()
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
    public async Task FirstRunSeed_EmptyTestConfigsTable_CreatesThreeConfigsWithThreeSectionRulesEach()
    {
        // Arrange
        var initializer = CreateInitializer();

        // Act
        await InvokeSeedTestConfigsAsync(initializer);

        // Assert
        await using var verifyContext = new ApplicationDbContext(_options);
        var configs = await verifyContext.TestConfigs
            .Include(c => c.SectionRules)
            .ToListAsync();

        Assert.Equal(ExpectedConfigCount, configs.Count);

        var codes = configs.Select(c => c.Code).OrderBy(c => c).ToList();
        Assert.Equal(new[] { LicenceCode.Code1, LicenceCode.Code2, LicenceCode.Code3 }.OrderBy(c => c), codes);

        Assert.All(configs, c => Assert.Equal(ExpectedSectionRulesPerConfig, c.SectionRules.Count));

        // Guards against a rotation/swap-type FK wiring bug: because all three seeded
        // TestConfigs currently carry identical SectionRule content, aggregate count/content
        // checks alone would still pass even if a SectionRule's TestConfigId ended up pointing
        // at the wrong parent TestConfig. Assert the FK explicitly, per row.
        Assert.All(configs, c => Assert.All(c.SectionRules, r => Assert.Equal(c.Id, r.TestConfigId)));
    }

    [Fact]
    public async Task SecondRunSeed_TestConfigsAlreadyExist_IsNoOpWithoutDuplicates()
    {
        // Arrange: first run seeds the configs.
        var firstRunInitializer = CreateInitializer();
        await InvokeSeedTestConfigsAsync(firstRunInitializer);

        // Act: simulate an application restart - a brand-new initializer instance, backed by
        // the same (already-populated) store, runs the seed step again.
        var restartInitializer = CreateInitializer();
        var exception = await Record.ExceptionAsync(() => InvokeSeedTestConfigsAsync(restartInitializer));

        // Assert
        Assert.Null(exception);

        await using var verifyContext = new ApplicationDbContext(_options);
        var configs = await verifyContext.TestConfigs.Include(c => c.SectionRules).ToListAsync();

        Assert.Equal(ExpectedConfigCount, configs.Count);
        Assert.All(configs, c => Assert.Equal(ExpectedSectionRulesPerConfig, c.SectionRules.Count));
    }

    [Fact]
    public async Task SeededValues_AfterSeeding_MatchDocumentedPlaceholders()
    {
        // Arrange
        var initializer = CreateInitializer();

        // Act
        await InvokeSeedTestConfigsAsync(initializer);

        // Assert
        await using var verifyContext = new ApplicationDbContext(_options);
        var configs = await verifyContext.TestConfigs.Include(c => c.SectionRules).ToListAsync();

        Assert.All(configs, config =>
        {
            Assert.Equal(ExpectedTimeLimitMinutes, config.TimeLimitMinutes);

            var rules = config.SectionRules.ToDictionary(r => r.Section);
            Assert.Equal(3, rules.Count);

            // Guards against a rotation/swap-type FK wiring bug: since all three seeded
            // TestConfigs carry identical SectionRule content, per-value checks alone can't
            // detect a SectionRule wired to the wrong parent TestConfig. Assert the FK
            // explicitly, per row.
            Assert.All(config.SectionRules, r => Assert.Equal(config.Id, r.TestConfigId));

            Assert.Equal(30, rules[SectionType.Rules].QuestionCount);
            Assert.Equal(22, rules[SectionType.Rules].PassMark);

            Assert.Equal(30, rules[SectionType.Signs].QuestionCount);
            Assert.Equal(23, rules[SectionType.Signs].PassMark);

            Assert.Equal(12, rules[SectionType.VehicleControls].QuestionCount);
            Assert.Equal(10, rules[SectionType.VehicleControls].PassMark);
        });
    }

    [Fact]
    public async Task ConfigValueChangedAfterSeeding_DirectDataStoreUpdate_IsReflectedOnSubsequentReadWithNoCodeChange()
    {
        // Arrange: seed, then change a value directly in the data store (simulating a
        // maintainer editing a row without touching any code).
        var initializer = CreateInitializer();
        await InvokeSeedTestConfigsAsync(initializer);

        await using (var mutateContext = new ApplicationDbContext(_options))
        {
            var rulesToBump = await mutateContext.SectionRules
                .Where(r => r.Section == SectionType.Rules)
                .ToListAsync();

            foreach (var rule in rulesToBump)
            {
                rule.PassMark = 25;
            }

            await mutateContext.SaveChangesAsync();
        }

        // Act: a subsequent read via a fresh context (standing in for
        // IApplicationDbContextFactory's per-operation context) picks up the new value.
        await using var verifyContext = new ApplicationDbContext(_options);
        var updatedRules = await verifyContext.SectionRules
            .Where(r => r.Section == SectionType.Rules)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(updatedRules);
        Assert.All(updatedRules, r => Assert.Equal(25, r.PassMark));
    }

    private ApplicationDbContextInitializer CreateInitializer()
    {
        var factoryMock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factoryMock.Setup(f => f.CreateDbContext()).Returns(() => new ApplicationDbContext(_options));

        var loggerMock = new Mock<ILogger<ApplicationDbContextInitializer>>();

        // SeedTestConfigsAsync never touches UserManager/RoleManager - these are only present
        // to satisfy the initializer's constructor and are never invoked in the code path
        // under test.
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

    private static async Task InvokeSeedTestConfigsAsync(ApplicationDbContextInitializer initializer)
    {
        var method = typeof(ApplicationDbContextInitializer)
            .GetMethod("SeedTestConfigsAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        var task = (Task)method!.Invoke(initializer, null)!;
        await task;
    }
}
