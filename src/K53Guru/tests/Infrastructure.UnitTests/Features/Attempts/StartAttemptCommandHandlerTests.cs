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
/// Covers spec-3-3-start-single-code-attempt.md's and spec-3-4-compose-combination-sittings.md's
/// start-side I/O &amp; Edge-Case Matrix rows directly against the production
/// StartAttemptCommandHandler, mirroring AddEditTestCommandHandlerTests.cs's SQLite in-memory
/// harness (no live MSSQL/PostgreSQL instance is reachable in this sandbox).
///
/// Matrix rows covered:
///   - Start, valid single-code Test -&gt; Attempt with 3 sections' worth of AttemptQuestions in
///     fixed section order, DisplayOrder 1..N
///   - Start, two attempts of the same Test -&gt; selection and/or order differ
///   - Start, Test not found -&gt; rejected, nothing persisted
///   - Start, Test not published -&gt; rejected, nothing persisted
///   - Start, insufficient pool in a section -&gt; rejected, names the under-provisioned section,
///     nothing persisted
///   - Start, new learner -&gt; a LearnerProfile row is created alongside the Attempt
///   - Start, valid Code1+2/Code1+3 combination (spec-3-4) -&gt; Rules+Signs drawn once
///     (AttemptQuestion.Code = the whole combination), then one VehicleControls module per
///     constituent code in fixed order (AttemptQuestion.Code = that one code)
///   - Start, Code2+3 or all-three combination (spec-3-4) -&gt; rejected, nothing persisted
///   - Start, combination with insufficient pool in one code's VehicleControls (spec-3-4) ->
///     rejected, message names both the section and the short code, nothing persisted
///   - Start, single-code Test regression (spec-3-4) -&gt; composes exactly as Story 3.3,
///     AttemptQuestion.Code set to that single code throughout
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

    private static Question NewQuestion(string stem, SectionType section, LicenceCode codes = LicenceCode.Code1) => new()
    {
        Stem = stem,
        Codes = codes,
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

    /// <summary>
    /// Seeds a published COMBINATION Test (Codes = the bitwise-OR of <paramref name="constituentCodes"/>)
    /// whose curated TestQuestions pool has <paramref name="rulesSignsPoolCount"/> Rules and Signs
    /// questions each (shared - not filtered by code), plus a per-constituent-code VehicleControls
    /// pool sized from <paramref name="vehicleControlsPoolCountByCode"/> (each such question is
    /// tagged with ONLY that one code, mirroring properly-curated admin content). A matching
    /// TestConfig (Rules/Signs requiring <paramref name="rulesSignsRequired"/>, VehicleControls
    /// requiring that code's own value from <paramref name="vehicleControlsRequiredByCode"/>) is
    /// seeded for EVERY constituent code, since composition looks up each constituent code's own
    /// TestConfig independently. Per-code VehicleControls requirements are intentionally NOT forced
    /// uniform, so a regression that silently reused the primary code's SectionRule for a
    /// non-primary code is independently catchable by a test that seeds differing values here.
    /// </summary>
    private async Task<int> SeedPublishedCombinationTestAsync(
        IReadOnlyList<LicenceCode> constituentCodes,
        int rulesSignsPoolCount,
        int rulesSignsRequired,
        IReadOnlyDictionary<LicenceCode, int> vehicleControlsPoolCountByCode,
        IReadOnlyDictionary<LicenceCode, int> vehicleControlsRequiredByCode)
    {
        await using var context = new ApplicationDbContext(_options);

        var testCodes = constituentCodes.Aggregate(LicenceCode.None, (acc, c) => acc | c);

        var questions = new List<Question>();
        foreach (var section in new[] { SectionType.Rules, SectionType.Signs })
        {
            for (var i = 0; i < rulesSignsPoolCount; i++)
            {
                questions.Add(NewQuestion($"{section} Q{i}", section, testCodes));
            }
        }

        foreach (var code in constituentCodes)
        {
            var count = vehicleControlsPoolCountByCode[code];
            for (var i = 0; i < count; i++)
            {
                questions.Add(NewQuestion($"VehicleControls {code} Q{i}", SectionType.VehicleControls, code));
            }
        }

        var test = new Test
        {
            Name = "Combination Test",
            Codes = testCodes,
            Sections = TestSectionScope.Rules | TestSectionScope.Signs | TestSectionScope.VehicleControls,
            Status = TestStatus.Published,
            TestQuestions = questions.Select(q => new TestQuestion { Question = q }).ToList()
        };
        context.Tests.Add(test);

        foreach (var code in constituentCodes)
        {
            context.TestConfigs.Add(new TestConfig
            {
                Code = code,
                TimeLimitMinutes = 60,
                SectionRules = new List<SectionRule>
                {
                    new() { Section = SectionType.Rules, QuestionCount = rulesSignsRequired, PassMark = 1 },
                    new() { Section = SectionType.Signs, QuestionCount = rulesSignsRequired, PassMark = 1 },
                    new() { Section = SectionType.VehicleControls, QuestionCount = vehicleControlsRequiredByCode[code], PassMark = 1 }
                }
            });
        }

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

    /// <summary>
    /// Seeds a published Test whose Codes is an UNSUPPORTED combination (Code2+3 or all-three) -
    /// used only to exercise the allowlist rejection path, so no TestConfig/TestQuestions pool is
    /// needed (rejection happens before either is read).
    /// </summary>
    private async Task<int> SeedPublishedTestWithUnsupportedCodesAsync(LicenceCode codes)
    {
        await using var context = new ApplicationDbContext(_options);
        var test = new Test
        {
            Name = "Unsupported Codes Test",
            Codes = codes,
            Sections = TestSectionScope.Rules,
            Status = TestStatus.Published
        };
        context.Tests.Add(test);
        await context.SaveChangesAsync();
        return test.Id;
    }

    [Fact]
    public async Task Start_Code2AndCode3Combination_RejectedWithClearMessage_NothingPersisted()
    {
        // Arrange: Code2+3 is explicitly on the "reject" side of the 5-value allowlist
        // (spec-3-4-compose-combination-sittings.md).
        var testId = await SeedPublishedTestWithUnsupportedCodesAsync(LicenceCode.Code2 | LicenceCode.Code3);
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Attempts.CountAsync());
    }

    [Fact]
    public async Task Start_AllThreeCodesCombination_RejectedWithClearMessage_NothingPersisted()
    {
        // Arrange: all-three is explicitly on the "reject" side of the 5-value allowlist.
        var testId = await SeedPublishedTestWithUnsupportedCodesAsync(
            LicenceCode.Code1 | LicenceCode.Code2 | LicenceCode.Code3);
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Attempts.CountAsync());
    }

    [Fact]
    public async Task Start_ValidCode1AndCode2Combination_ComposesSharedRulesSignsPlusPerCodeVehicleControlsModulesInFixedOrder()
    {
        // Arrange: Rules/Signs pool of 5 (3 required, shared). VehicleControls requirements are
        // DELIBERATELY DIFFERENT per code (Code1 needs 2, Code2 needs 4) - both pools sized to
        // comfortably exceed their own code's requirement. If composition ever silently reused
        // Code1's SectionRule for Code2 (instead of loading Code2's own), Code2's module would come
        // out at 2 questions instead of 4 and the count assertions below would catch it.
        var testId = await SeedPublishedCombinationTestAsync(
            constituentCodes: new[] { LicenceCode.Code1, LicenceCode.Code2 },
            rulesSignsPoolCount: 5,
            rulesSignsRequired: 3,
            vehicleControlsPoolCountByCode: new Dictionary<LicenceCode, int>
            {
                [LicenceCode.Code1] = 5,
                [LicenceCode.Code2] = 6
            },
            vehicleControlsRequiredByCode: new Dictionary<LicenceCode, int>
            {
                [LicenceCode.Code1] = 2,
                [LicenceCode.Code2] = 4
            });
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.Equal(LicenceCode.Code1 | LicenceCode.Code2, dto.Code);

        // 3 Rules + 3 Signs (shared) + 2 Code1 VehicleControls + 4 Code2 VehicleControls = 12.
        Assert.Equal(12, dto.AttemptQuestions.Count);
        Assert.Equal(Enumerable.Range(1, 12), dto.AttemptQuestions.Select(q => q.DisplayOrder));

        // Fixed order: Rules, Signs, then Code1's VehicleControls module, then Code2's.
        Assert.Equal(
            new[] { SectionType.Rules, SectionType.Rules, SectionType.Rules,
                    SectionType.Signs, SectionType.Signs, SectionType.Signs,
                    SectionType.VehicleControls, SectionType.VehicleControls,
                    SectionType.VehicleControls, SectionType.VehicleControls,
                    SectionType.VehicleControls, SectionType.VehicleControls },
            dto.AttemptQuestions.Select(q => q.Section));

        // Rules/Signs questions carry the FULL combination as their Code; each VehicleControls
        // module's questions carry only that ONE constituent code.
        var rulesAndSigns = dto.AttemptQuestions.Take(6).ToList();
        Assert.All(rulesAndSigns, q => Assert.Equal(LicenceCode.Code1 | LicenceCode.Code2, q.Code));

        var vehicleControls = dto.AttemptQuestions.Skip(6).ToList();
        // Code1's module is exactly 2 questions (its own requirement) - not Code2's 4.
        Assert.Equal(2, vehicleControls.Count(q => q.Code == LicenceCode.Code1));
        Assert.All(vehicleControls.Take(2), q => Assert.Equal(LicenceCode.Code1, q.Code));
        // Code2's module is exactly 4 questions (its own requirement) - not Code1's 2.
        Assert.Equal(4, vehicleControls.Count(q => q.Code == LicenceCode.Code2));
        Assert.All(vehicleControls.Skip(2).Take(4), q => Assert.Equal(LicenceCode.Code2, q.Code));

        // DB round-trip check: read the persisted Attempt back from a FRESH context (not the
        // in-memory mapped DTO) and confirm the Code values written to the database match what was
        // expected - Rules/Signs rows carry the full combination, each VehicleControls row carries
        // only its own constituent code.
        await using var verifyContext = new ApplicationDbContext(_options);
        var savedAttempt = await verifyContext.Attempts
            .Include(a => a.AttemptQuestions)
            .SingleAsync(a => a.Id == dto.Id);
        Assert.Equal(12, savedAttempt.AttemptQuestions.Count);

        var savedOrdered = savedAttempt.AttemptQuestions.OrderBy(q => q.DisplayOrder).ToList();
        var savedRulesAndSigns = savedOrdered.Take(6).ToList();
        Assert.All(savedRulesAndSigns, q => Assert.Equal(LicenceCode.Code1 | LicenceCode.Code2, q.Code));

        var savedVehicleControls = savedOrdered.Skip(6).ToList();
        Assert.Equal(2, savedVehicleControls.Count(q => q.Code == LicenceCode.Code1));
        Assert.All(savedVehicleControls.Take(2), q => Assert.Equal(LicenceCode.Code1, q.Code));
        Assert.Equal(4, savedVehicleControls.Count(q => q.Code == LicenceCode.Code2));
        Assert.All(savedVehicleControls.Skip(2).Take(4), q => Assert.Equal(LicenceCode.Code2, q.Code));
    }

    [Fact]
    public async Task Start_ValidCode1AndCode3Combination_ComposesSharedRulesSignsPlusPerCodeVehicleControlsModulesInFixedOrder()
    {
        // Arrange: same shape as the Code1+2 case, but Code1+3 - proves the composition loop isn't
        // hardcoded to Code1/Code2 specifically.
        var testId = await SeedPublishedCombinationTestAsync(
            constituentCodes: new[] { LicenceCode.Code1, LicenceCode.Code3 },
            rulesSignsPoolCount: 5,
            rulesSignsRequired: 3,
            vehicleControlsPoolCountByCode: new Dictionary<LicenceCode, int>
            {
                [LicenceCode.Code1] = 4,
                [LicenceCode.Code3] = 4
            },
            vehicleControlsRequiredByCode: new Dictionary<LicenceCode, int>
            {
                [LicenceCode.Code1] = 2,
                [LicenceCode.Code3] = 2
            });
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.Equal(LicenceCode.Code1 | LicenceCode.Code3, dto.Code);
        Assert.Equal(10, dto.AttemptQuestions.Count);
        Assert.Equal(
            new[] { SectionType.Rules, SectionType.Rules, SectionType.Rules,
                    SectionType.Signs, SectionType.Signs, SectionType.Signs,
                    SectionType.VehicleControls, SectionType.VehicleControls,
                    SectionType.VehicleControls, SectionType.VehicleControls },
            dto.AttemptQuestions.Select(q => q.Section));

        var vehicleControls = dto.AttemptQuestions.Skip(6).ToList();
        Assert.All(vehicleControls.Take(2), q => Assert.Equal(LicenceCode.Code1, q.Code));
        Assert.All(vehicleControls.Skip(2).Take(2), q => Assert.Equal(LicenceCode.Code3, q.Code));
    }

    [Fact]
    public async Task Start_CombinationWithInsufficientPoolInOneCodesVehicleControls_RejectedNamingSectionAndCode_NothingPersisted()
    {
        // Arrange: Code1's VehicleControls module has plenty of questions (5 available, 3
        // required); Code2's has too few (1 available, 3 required). Rules/Signs are both
        // sufficient. The rejection must name BOTH the VehicleControls section and the short code
        // (Code2) that is short - not just the section.
        var testId = await SeedPublishedCombinationTestAsync(
            constituentCodes: new[] { LicenceCode.Code1, LicenceCode.Code2 },
            rulesSignsPoolCount: 5,
            rulesSignsRequired: 3,
            vehicleControlsPoolCountByCode: new Dictionary<LicenceCode, int>
            {
                [LicenceCode.Code1] = 5,
                [LicenceCode.Code2] = 1
            },
            vehicleControlsRequiredByCode: new Dictionary<LicenceCode, int>
            {
                [LicenceCode.Code1] = 3,
                [LicenceCode.Code2] = 3
            });
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(nameof(SectionType.VehicleControls), result.ErrorMessage);
        Assert.Contains(nameof(LicenceCode.Code2), result.ErrorMessage);

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Attempts.CountAsync());
    }

    [Fact]
    public async Task Start_SingleCodeTestRegression_ComposesExactlyAsStory3_3WithAttemptQuestionCodeSetThroughout()
    {
        // Arrange: identical single-code (Code1) setup to
        // Start_ValidSingleCodeTest_ComposesAttemptWithFixedSectionOrderAndSequentialDisplayOrder -
        // proves spec-3-4's generalized composition loop is a strict generalization for the
        // single-code path, not a behavior change, AND that AttemptQuestion.Code is now populated
        // (set to the single code) for every question, Rules/Signs/VehicleControls alike.
        var testId = await SeedPublishedTestAsync(perSectionCount: 5, required: 3);
        var handler = new StartAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new StartAttemptCommand { LearnerProfileId = Guid.NewGuid(), TestId = testId },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.Equal(LicenceCode.Code1, dto.Code);
        Assert.Equal(9, dto.AttemptQuestions.Count);
        Assert.All(dto.AttemptQuestions, q => Assert.Equal(LicenceCode.Code1, q.Code));

        // DisplayOrder is 1..9, globally sequential - proves this test is self-contained proof of
        // the regression claim rather than depending on a separate, older test that doesn't check
        // the Code field.
        Assert.Equal(Enumerable.Range(1, 9), dto.AttemptQuestions.Select(q => q.DisplayOrder));

        // Section order is fixed: Rules(3) -> Signs(3) -> VehicleControls(3), unchanged from
        // Story 3.3.
        Assert.Equal(
            new[] { SectionType.Rules, SectionType.Rules, SectionType.Rules,
                    SectionType.Signs, SectionType.Signs, SectionType.Signs,
                    SectionType.VehicleControls, SectionType.VehicleControls, SectionType.VehicleControls },
            dto.AttemptQuestions.Select(q => q.Section));
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
