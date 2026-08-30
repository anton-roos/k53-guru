using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using K53Guru.Infrastructure.Persistence;
using K53Guru.Infrastructure.Persistence.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ValidationException = FluentValidation.ValidationException;

namespace K53Guru.Infrastructure.UnitTests.Persistence;

/// <summary>
/// Covers spec-1-3-question-content-model.md I/O &amp; Edge-Case Matrix rows:
///   - "Valid question, resolved sign"
///   - "Valid question, no sign"
///   - "Unresolved sign_ref"
///   - "Zero correct answers"
///   - "Multiple correct answers"
///   - "Multiple applicable codes"
///
/// The "Migration applied" matrix row is covered separately by
/// AddQuestionContentModelMigrationTests.cs, which exercises the real generated migration file
/// directly. This class additionally covers one save-time invariant that isn't its own matrix
/// row but belongs to the same "reject invalid Question at save time" family: a Question left at
/// the default Codes (LicenceCode.None) must be rejected, per the frozen Intent's requirement
/// that a Question carry "one or more applicable codes".
///
/// Uses a shared-connection SQLite in-memory database (mirroring RoadSignSeedingTests.cs /
/// RoadSignConfigurationTests.cs) since no live PostgreSQL instance is reachable in this
/// sandbox. The schema is derived from ApplicationDbContext's fluent configuration
/// (QuestionConfiguration/AnswerOptionConfiguration), and QuestionValidationInterceptor is
/// registered directly on the DbContextOptions so the real interceptor code path is exercised.
/// </summary>
public class QuestionValidationInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public QuestionValidationInterceptorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new QuestionValidationInterceptor())
            .Options;

        using var context = new ApplicationDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private static Question BuildQuestion(
        string? signRef = null,
        LicenceCode codes = LicenceCode.Code1,
        SectionType section = SectionType.Rules,
        List<AnswerOption>? answerOptions = null) => new()
    {
        Stem = "Sample question stem",
        Codes = codes,
        Section = section,
        LanguageCode = "en",
        SignRef = signRef,
        AnswerOptions = answerOptions ?? new List<AnswerOption>
        {
            new() { Text = "Correct", IsCorrect = true, Order = 1 },
            new() { Text = "Wrong", IsCorrect = false, Order = 2 }
        }
    };

    [Fact]
    public async Task ValidQuestion_ResolvedSign_SavesAndRoundTripsAllFieldsIntact()
    {
        // Arrange
        await using (var seedContext = CreateContext())
        {
            seedContext.RoadSigns.Add(new RoadSign { LegislationCode = "R1", Description = "Stop sign" });
            await seedContext.SaveChangesAsync();
        }

        var question = BuildQuestion(signRef: "R1", section: SectionType.Signs);

        // Act
        await using (var context = CreateContext())
        {
            context.Questions.Add(question);
            await context.SaveChangesAsync();
        }

        // Assert
        await using var verifyContext = CreateContext();
        var saved = await verifyContext.Questions.Include(q => q.AnswerOptions).SingleAsync();

        Assert.Equal("Sample question stem", saved.Stem);
        Assert.Equal(LicenceCode.Code1, saved.Codes);
        Assert.Equal(SectionType.Signs, saved.Section);
        Assert.Equal("en", saved.LanguageCode);
        Assert.Equal("R1", saved.SignRef);
        Assert.Equal(2, saved.AnswerOptions.Count);
        Assert.Single(saved.AnswerOptions, a => a.IsCorrect);
    }

    [Fact]
    public async Task ValidQuestion_NoSign_SavesSuccessfully_SkipsSignResolutionCheck()
    {
        // Arrange
        var question = BuildQuestion(signRef: null, section: SectionType.Rules);

        // Act
        await using var context = CreateContext();
        context.Questions.Add(question);
        var exception = await Record.ExceptionAsync(() => context.SaveChangesAsync());

        // Assert
        Assert.Null(exception);

        await using var verifyContext = CreateContext();
        var saved = await verifyContext.Questions.SingleAsync();
        Assert.Null(saved.SignRef);
    }

    [Fact]
    public async Task UnresolvedSignRef_SaveRejectedBeforeCommit_ThrowsWithUnresolvedMessage()
    {
        // Arrange: no RoadSign seeded, so "R404" cannot resolve.
        var question = BuildQuestion(signRef: "R404");

        // Act
        await using var context = CreateContext();
        context.Questions.Add(question);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.SaveChangesAsync());

        // Assert
        Assert.Contains("unresolved", exception.Message, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = CreateContext();
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    // Note: the interceptor also throws an "ambiguous" ValidationException when sign_ref
    // resolves to more than one RoadSign (see QuestionValidationInterceptor). That branch isn't
    // a row in this spec's I/O & Edge-Case Matrix and can't be reached through this schema, since
    // RoadSignConfiguration's unique index on LegislationCode (Story 1.1) already makes more than
    // one match impossible to persist - it exists purely as a defensive guard.

    [Fact]
    public async Task ZeroCorrectAnswers_SaveRejectedBeforeCommit_ThrowsWithExactlyOneCorrectMessage()
    {
        // Arrange
        var question = BuildQuestion(answerOptions: new List<AnswerOption>
        {
            new() { Text = "Wrong A", IsCorrect = false, Order = 1 },
            new() { Text = "Wrong B", IsCorrect = false, Order = 2 }
        });

        // Act
        await using var context = CreateContext();
        context.Questions.Add(question);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.SaveChangesAsync());

        // Assert
        Assert.Contains("exactly one correct", exception.Message, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = CreateContext();
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Fact]
    public async Task MultipleCorrectAnswers_SaveRejectedBeforeCommit_ThrowsWithExactlyOneCorrectMessage()
    {
        // Arrange
        var question = BuildQuestion(answerOptions: new List<AnswerOption>
        {
            new() { Text = "Correct A", IsCorrect = true, Order = 1 },
            new() { Text = "Correct B", IsCorrect = true, Order = 2 }
        });

        // Act
        await using var context = CreateContext();
        context.Questions.Add(question);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.SaveChangesAsync());

        // Assert
        Assert.Contains("exactly one correct", exception.Message, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = CreateContext();
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Fact]
    public async Task NoneCode_SaveRejectedBeforeCommit_ThrowsWithApplicableLicenceCodeMessage()
    {
        // Arrange: Codes left at its default (LicenceCode.None) - zero bits set.
        var question = BuildQuestion(codes: LicenceCode.None);

        // Act
        await using var context = CreateContext();
        context.Questions.Add(question);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.SaveChangesAsync());

        // Assert
        Assert.Contains("applicable licence code", exception.Message, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = CreateContext();
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Fact]
    public async Task MultipleApplicableCodes_SavesAndRoundTripsAsCombinedFlagValue()
    {
        // Arrange
        var question = BuildQuestion(codes: LicenceCode.Code1 | LicenceCode.Code2);

        // Act
        await using (var context = CreateContext())
        {
            context.Questions.Add(question);
            await context.SaveChangesAsync();
        }

        // Assert
        await using var verifyContext = CreateContext();
        var saved = await verifyContext.Questions.SingleAsync();
        Assert.Equal(LicenceCode.Code1 | LicenceCode.Code2, saved.Codes);
        Assert.True(saved.Codes.HasFlag(LicenceCode.Code1));
        Assert.True(saved.Codes.HasFlag(LicenceCode.Code2));
        Assert.False(saved.Codes.HasFlag(LicenceCode.Code3));
    }

    [Fact]
    public async Task SchemaFromEfModel_QuestionsAndAnswerOptionsTablesExist_AndForeignKeyIsEnforced()
    {
        // Supplementary to AddQuestionContentModelMigrationTests.cs (which exercises the actual
        // migration file): this asserts the same EF model that migration is generated from
        // produces both tables and a live, enforced FK when actually run against a database.
        await using var context = CreateContext();

        var tableNames = new List<string>();
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tableNames.Add(reader.GetString(0));
            }
        }

        // Table names are the default EF Core identifiers here since UseSnakeCaseNamingConvention()
        // is only applied to the Npgsql provider at runtime (see DependencyInjection.UseDatabase),
        // not to this test's plain SQLite options - the real PostgreSQL migration uses
        // "questions"/"answer_options" instead, generated from this same EF model.
        Assert.Contains("Questions", tableNames);
        Assert.Contains("AnswerOptions", tableNames);

        // Assert: the FK relationship is enforced - an AnswerOption referencing a non-existent
        // QuestionId cannot be committed.
        context.AnswerOptions.Add(new AnswerOption { QuestionId = 9999, Text = "Orphan", IsCorrect = true, Order = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
