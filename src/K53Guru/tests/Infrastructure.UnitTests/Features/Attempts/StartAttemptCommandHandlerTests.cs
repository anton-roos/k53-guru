using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.Attempts.Commands.Start;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.Attempts;

/// <summary>
/// Covers spec-3-3-start-single-code-attempt.md's start-side I/O &amp; Edge-Case Matrix rows
/// directly against the production StartAttemptCommandHandler, mirroring
/// AddEditTestCommandHandlerTests.cs's SQLite in-memory harness (no live MSSQL/PostgreSQL instance
/// is reachable in this sandbox).
///
/// Matrix rows covered:
///   - Start, valid single-code Test -&gt; Attempt with 3 sections' worth of AttemptQuestions in
///     fixed section order, DisplayOrder 1..N
///   - Start, two attempts of the same Test -&gt; selection and/or order differ
///   - Start, Test not found -&gt; rejected, nothing persisted
///   - Start, Test not published -&gt; rejected, nothing persisted
///   - Start, Test is a combination -&gt; rejected, nothing persisted
///   - Start, insufficient pool in a section -&gt; rejected, names the under-provisioned section,
///     nothing persisted
///   - Start, new learner -&gt; a LearnerProfile row is created alongside the Attempt
/// </summary>
public class StartAttemptCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IMapper _mapper;

    public StartAttemptCommandHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var schemaContext = new ApplicationDbContext(_options))
        {
            schemaContext.Database.EnsureCreated();
        }

        var mapperConfiguration =
            new MapperConfiguration(cfg => cfg.AddMaps(Assembly.GetAssembly(typeof(StartAttemptCommand))));
        _mapper = mapperConfiguration.CreateMapper();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private IApplicationDbContextFactory CreateFactory()
    {
        var factoryMock = new Mock<IApplicationDbContextFactory>();
        factoryMock
            .Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (IApplicationDbContext)new ApplicationDbContext(_options));
        return factoryMock.Object;
    }

    private static Question NewQuestion(string stem, SectionType section) => new()
    {
        Stem = stem,
        Codes = LicenceCode.Code1,
        Section = section,
        LanguageCode = "en",
        AnswerOptions = new List<AnswerOption>
        {
            new() { Text = "Correct answer", IsCorrect = true, Order = 0 },
            new() { Text = "Wrong answer", IsCorrect = false, Order = 1 }
        }
    };

    /// <summary>
    /// Seeds a published, single-code (Code1) Test whose curated TestQuestions pool has
    /// <paramref name="perSectionCount"/> questions in each of Rules/Signs/VehicleControls, plus a
    /// matching TestConfig requiring exactly <paramref name="required"/> per section
    /// (required &lt;= perSectionCount).
    /// </summary>
    private async Task<int> SeedPublishedTestAsync(int perSectionCount, int required, TestStatus status = TestStatus.Published)
    {
        await using var context = new ApplicationDbContext(_options);

        var questions = new List<Question>();
        foreach (var section in new[] { SectionType.Rules, SectionType.Signs, SectionType.VehicleControls })
        {
            for (var i = 0; i < perSectionCount; i++)
            {
                questions.Add(NewQuestion($"{section} Q{i}", section));
            }
        }

        var test = new Test
        {
            Name = "Sample Test",
            Codes = LicenceCode.Code1,
            Sections = TestSectionScope.Rules | TestSectionScope.Signs | TestSectionScope.VehicleControls,
            Status = status,
            TestQuestions = questions.Select(q => new TestQuestion { Question = q }).ToList()
        };
        context.Tests.Add(test);

        context.TestConfigs.Add(new TestConfig
        {
            Code = LicenceCode.Code1,
            TimeLimitMinutes = 60,
            SectionRules = new List<SectionRule>
            {
                new() { Section = SectionType.Rules, QuestionCount = required, PassMark = 1 },
                new() { Section = SectionType.Signs, QuestionCount = required, PassMark = 1 },
                new() { Section = SectionType.VehicleControls, QuestionCount = required, PassMark = 1 }
            }
        });

        await context.SaveChangesAsync();
        return test.Id;
    }

    /// <summary>
    /// Seeds a published, single-code (Code1) Test whose curated TestQuestions pool has a
    /// PER-SECTION pool count taken from <paramref name="poolCountsBySection"/> (so different
    /// sections can be provisioned differently), plus a matching TestConfig requiring exactly
    /// <paramref name="required"/> per section for all three sections.
    /// </summary>
    private async Task<int> SeedPublishedTestWithPerSectionPoolAsync(
        IReadOnlyDictionary<SectionType, int> poolCountsBySection, int required)
    {
        await using var context = new ApplicationDbContext(_options);

        var questions = new List<Question>();
        foreach (var (section, count) in poolCountsBySection)
        {
            for (var i = 0; i < count; i++)
            {
                questions.Add(NewQuestion($"{section} Q{i}", section));
            }
        }

        var test = new Test
        {
            Name = "Sample Test",
            Codes = LicenceCode.Code1,
            Sections = TestSectionScope.Rules | TestSectionScope.Signs | TestSectionScope.VehicleControls,
            Status = TestStatus.Published,
            TestQuestions = questions.Select(q => new TestQuestion { Question = q }).ToList()
        };
        context.Tests.Add(test);

        context.TestConfigs.Add(new TestConfig
        {
            Code = LicenceCode.Code1,
            TimeLimitMinutes = 60,
            SectionRules = new List<SectionRule>
            {
                new() { Section = SectionType.Rules, QuestionCount = required, PassMark = 1 },
                new() { Section = SectionType.Signs, QuestionCount = required, PassMark = 1 },
                new() { Section = SectionType.VehicleControls, QuestionCount = required, PassMark = 1 }
            }
        });

        await context.SaveChangesAsync();
        return test.Id;
    }

    [Fact]
    public async Task Start_ValidSingleCodeTest_ComposesAttemptWithFixedSectionOrderAndSequentialDisplayOrder()
    {
        // Arrange: 5 questions per section, config requires 3 per section -> 9 total.
        var testId = await SeedPublishedTestAsync(perSectionCount: 5, required: 3);
        var learnerProfileId = Guid.NewGuid();
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = learnerProfileId, TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.Equal(LicenceCode.Code1, dto.Code);
        Assert.Equal(9, dto.AttemptQuestions.Count);

        // DisplayOrder is 1..N, globally sequential.
        Assert.Equal(Enumerable.Range(1, 9), dto.AttemptQuestions.Select(q => q.DisplayOrder));

        // Section order is fixed: Rules -> Signs -> VehicleControls.
        Assert.Equal(
            new[] { SectionType.Rules, SectionType.Rules, SectionType.Rules,
                    SectionType.Signs, SectionType.Signs, SectionType.Signs,
                    SectionType.VehicleControls, SectionType.VehicleControls, SectionType.VehicleControls },
            dto.AttemptQuestions.Select(q => q.Section));

        // Each question carries its snapshotted content and ordered answer options.
        foreach (var q in dto.AttemptQuestions)
        {
            Assert.False(string.IsNullOrEmpty(q.Stem));
            Assert.Equal(2, q.AttemptAnswerOptions.Count);
            Assert.Equal(new[] { 0, 1 }, q.AttemptAnswerOptions.Select(o => o.Order));
        }

        await using var verifyContext = new ApplicationDbContext(_options);
        var savedAttempt = await verifyContext.Attempts
            .Include(a => a.AttemptQuestions).ThenInclude(q => q.AttemptAnswerOptions)
            .SingleAsync(a => a.Id == dto.Id);
        Assert.Equal(learnerProfileId, savedAttempt.LearnerProfileId);
        Assert.Equal(testId, savedAttempt.TestId);
        Assert.Equal(9, savedAttempt.AttemptQuestions.Count);
        Assert.All(savedAttempt.AttemptQuestions, q => Assert.Equal(2, q.AttemptAnswerOptions.Count));
    }

    [Fact]
    public async Task Start_TwoAttemptsOfSameTest_SelectionOrOrderDiffers()
    {
        // Arrange: a generous pool (10 per section, taking 5) makes an accidental identical
        // shuffle-and-selection between two independent attempts astronomically unlikely.
        var testId = await SeedPublishedTestAsync(perSectionCount: 10, required: 5);
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result1 = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);
        var result2 = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.True(result1.Succeeded);
        Assert.True(result2.Succeeded);

        var stems1 = result1.Data!.AttemptQuestions.Select(q => q.Stem).ToList();
        var stems2 = result2.Data!.AttemptQuestions.Select(q => q.Stem).ToList();

        Assert.NotEqual(stems1, stems2);
    }

    [Fact]
    public async Task Start_TwoAttemptsBySameLearner_SelectionOrOrderDiffers()
    {
        // Arrange: same rationale/pool size as Start_TwoAttemptsOfSameTest_SelectionOrOrderDiffers,
        // but both attempts are started by the SAME LearnerProfileId. Guards against a regression
        // that seeded the shuffle deterministically off LearnerProfileId - which would look random
        // across different learners (the other test) but be identical for repeat starts by the
        // same learner (undetected by that test alone).
        var testId = await SeedPublishedTestAsync(perSectionCount: 10, required: 5);
        var learnerProfileId = Guid.NewGuid();
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result1 = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = learnerProfileId, TestId = testId },
            CancellationToken.None);
        var result2 = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = learnerProfileId, TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.True(result1.Succeeded);
        Assert.True(result2.Succeeded);

        var stems1 = result1.Data!.AttemptQuestions.Select(q => q.Stem).ToList();
        var stems2 = result2.Data!.AttemptQuestions.Select(q => q.Stem).ToList();

        Assert.NotEqual(stems1, stems2);
    }

    [Fact]
    public async Task Start_PoolExactlyMatchesRequiredCount_ComposesSuccessfullyWithNoSurplus()
    {
        // Arrange: each section's pool has EXACTLY the required count (zero surplus) - proves the
        // `sectionPool.Count < rule.QuestionCount` boundary check doesn't incorrectly reject, and
        // the exact-match case still composes and counts correctly.
        var testId = await SeedPublishedTestAsync(perSectionCount: 4, required: 4);
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.Equal(12, dto.AttemptQuestions.Count);
        Assert.Equal(Enumerable.Range(1, 12), dto.AttemptQuestions.Select(q => q.DisplayOrder));
        Assert.Equal(4, dto.AttemptQuestions.Count(q => q.Section == SectionType.Rules));
        Assert.Equal(4, dto.AttemptQuestions.Count(q => q.Section == SectionType.Signs));
        Assert.Equal(4, dto.AttemptQuestions.Count(q => q.Section == SectionType.VehicleControls));
    }

    [Fact]
    public async Task Start_TestNotFound_RejectedWithClearMessage_NothingPersisted()
    {
        // Arrange
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = 12345 },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Attempts.CountAsync());
    }

    [Fact]
    public async Task Start_TestNotPublished_RejectedWithClearMessage_NothingPersisted()
    {
        // Arrange
        var testId = await SeedPublishedTestAsync(perSectionCount: 5, required: 3, status: TestStatus.Draft);
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("not published", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Attempts.CountAsync());
    }

    [Fact]
    public async Task Start_TestIsCombination_RejectedWithClearMessage_NothingPersisted()
    {
        // Arrange
        await using var context = new ApplicationDbContext(_options);
        var test = new Test
        {
            Name = "Combo Test",
            Codes = LicenceCode.Code1 | LicenceCode.Code2,
            Sections = TestSectionScope.Rules,
            Status = TestStatus.Published
        };
        context.Tests.Add(test);
        await context.SaveChangesAsync();

        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = test.Id },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("combination", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Attempts.CountAsync());
    }

    [Fact]
    public async Task Start_InsufficientPoolInASection_RejectedNamingTheSection_NothingPersisted()
    {
        // Arrange: ONLY VehicleControls is under-provisioned (2 available, 5 required); Rules and
        // Signs both have a sufficient pool. The handler's fixed iteration order is Rules first,
        // so seeding all three sections equally short (as a naive version of this test would) lets
        // a hardcoded "always report Rules" bug pass undetected. Starving a non-first section
        // instead proves the message-generation logic actually names the section that is short,
        // not just whichever one is checked first.
        var testId = await SeedPublishedTestWithPerSectionPoolAsync(
            new Dictionary<SectionType, int>
            {
                [SectionType.Rules] = 5,
                [SectionType.Signs] = 5,
                [SectionType.VehicleControls] = 2
            },
            required: 5);
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(nameof(SectionType.VehicleControls), result.ErrorMessage);
        Assert.DoesNotContain(nameof(SectionType.Rules), result.ErrorMessage);

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Attempts.CountAsync());
    }

    [Fact]
    public async Task Start_NewLearner_LearnerProfileRowIsCreatedAlongsideAttempt()
    {
        // Arrange
        var testId = await SeedPublishedTestAsync(perSectionCount: 3, required: 3);
        var learnerProfileId = Guid.NewGuid();
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = learnerProfileId, TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        var profile = await verifyContext.LearnerProfiles.SingleOrDefaultAsync(lp => lp.Id == learnerProfileId);
        Assert.NotNull(profile);
    }

    [Fact]
    public async Task Start_ExistingLearner_ReusesTheSameLearnerProfileRow_NoDuplicateCreated()
    {
        // Arrange
        var testId = await SeedPublishedTestAsync(perSectionCount: 3, required: 3);
        var learnerProfileId = Guid.NewGuid();
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        var first = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = learnerProfileId, TestId = testId },
            CancellationToken.None);
        Assert.True(first.Succeeded);

        // Act: start a second attempt for the same, now-existing learner.
        var second = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = learnerProfileId, TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.True(second.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(1, await verifyContext.LearnerProfiles.CountAsync(lp => lp.Id == learnerProfileId));
        Assert.Equal(2, await verifyContext.Attempts.CountAsync(a => a.LearnerProfileId == learnerProfileId));
    }
}
