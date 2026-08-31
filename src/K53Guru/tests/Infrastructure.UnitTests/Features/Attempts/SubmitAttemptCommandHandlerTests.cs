using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using K53Guru.Application.Common.ExceptionHandlers;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.Attempts.Commands.CheckAnswer;
using K53Guru.Application.Features.Attempts.Commands.Start;
using K53Guru.Application.Features.Attempts.Commands.Submit;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.Attempts;

/// <summary>
/// Covers spec-3-5-submit-and-grade-attempt.md's I/O &amp; Edge-Case Matrix rows directly against
/// the production SubmitAttemptCommandHandler, mirroring StartAttemptCommandHandlerTests.cs's and
/// GetAttemptQueryHandlerTests.cs's SQLite in-memory harness. Each test starts a real Attempt via
/// the production StartAttemptCommandHandler first, then submits against it, so the grading path
/// is exercised end-to-end against genuinely persisted/snapshotted data rather than hand-built
/// entities.
///
/// Matrix rows covered:
///   - Submit, single-code, all sections pass -&gt; Passed=true, one CodeResult, every
///     SectionResult.Passed=true
///   - Submit, single-code, one section fails -&gt; Passed=false, that section's
///     SectionResult.Passed=false, others true
///   - Submit, combination, partial pass -&gt; overall Passed=false; CodeResults show Code1
///     Passed=true, Code2 Passed=false
///   - Submit, attempt not found / wrong learner -&gt; rejected identically (NotFoundException),
///     same as Story 3.3's resume behavior
///   - Submit, already submitted -&gt; rejected, no re-grading
///   - Submit, unanswered question -&gt; graded as incorrect for that question, no crash
///
/// Plus one extra regression test for a Boundaries-documented behavior not itself a distinct
/// matrix row: an AttemptQuestionId/SelectedAttemptAnswerOptionId that doesn't belong to this
/// attempt is silently ignored, not an error.
/// </summary>
public class SubmitAttemptCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IMapper _mapper;

    public SubmitAttemptCommandHandlerTests()
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
            new MapperConfiguration(cfg => cfg.AddMaps(Assembly.GetAssembly(typeof(SubmitAttemptCommand))));
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
    /// Seeds a published, single-code Test with <paramref name="perSectionCount"/> questions per
    /// section and a matching TestConfig whose PassMark for each section comes from
    /// <paramref name="passMarkBySection"/> (question count fixed to perSectionCount, so
    /// correctCount can be controlled exactly by how many of a section's questions are answered
    /// correctly at submit time).
    /// </summary>
    private async Task<int> SeedPublishedTestAsync(
        int perSectionCount, IReadOnlyDictionary<SectionType, int> passMarkBySection, LicenceCode code = LicenceCode.Code1)
    {
        await using var context = new ApplicationDbContext(_options);

        var questions = new List<Question>();
        foreach (var section in new[] { SectionType.Rules, SectionType.Signs, SectionType.VehicleControls })
        {
            for (var i = 0; i < perSectionCount; i++)
            {
                questions.Add(NewQuestion($"{section} Q{i}", section, code));
            }
        }

        var test = new Test
        {
            Name = "Sample Test",
            Codes = code,
            Sections = TestSectionScope.Rules | TestSectionScope.Signs | TestSectionScope.VehicleControls,
            Status = TestStatus.Published,
            TestQuestions = questions.Select(q => new TestQuestion { Question = q }).ToList()
        };
        context.Tests.Add(test);

        context.TestConfigs.Add(new TestConfig
        {
            Code = code,
            TimeLimitMinutes = 60,
            SectionRules = new List<SectionRule>
            {
                new() { Section = SectionType.Rules, QuestionCount = perSectionCount, PassMark = passMarkBySection[SectionType.Rules] },
                new() { Section = SectionType.Signs, QuestionCount = perSectionCount, PassMark = passMarkBySection[SectionType.Signs] },
                new() { Section = SectionType.VehicleControls, QuestionCount = perSectionCount, PassMark = passMarkBySection[SectionType.VehicleControls] }
            }
        });

        await context.SaveChangesAsync();
        return test.Id;
    }

    /// <summary>
    /// Seeds a published Code1+Code2 combination Test: 1 shared Rules question and 1 shared Signs
    /// question (PassMark 1 each, shared identically by both codes' TestConfig), plus one
    /// VehicleControls question per code (PassMark 1 each, independently configurable via
    /// <paramref name="vehicleControlsQuestionCountByCode"/>/PassMark fixed at 1 per question
    /// answered correctly).
    /// </summary>
    private async Task<int> SeedPublishedCombinationTestAsync(
        IReadOnlyDictionary<LicenceCode, int> vehicleControlsQuestionCountByCode)
    {
        await using var context = new ApplicationDbContext(_options);

        var testCodes = LicenceCode.Code1 | LicenceCode.Code2;

        var questions = new List<Question>
        {
            NewQuestion("Rules Q0", SectionType.Rules, testCodes),
            NewQuestion("Signs Q0", SectionType.Signs, testCodes)
        };

        foreach (var (code, count) in vehicleControlsQuestionCountByCode)
        {
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

        foreach (var code in vehicleControlsQuestionCountByCode.Keys)
        {
            context.TestConfigs.Add(new TestConfig
            {
                Code = code,
                TimeLimitMinutes = 60,
                SectionRules = new List<SectionRule>
                {
                    new() { Section = SectionType.Rules, QuestionCount = 1, PassMark = 1 },
                    new() { Section = SectionType.Signs, QuestionCount = 1, PassMark = 1 },
                    new() { Section = SectionType.VehicleControls, QuestionCount = vehicleControlsQuestionCountByCode[code], PassMark = vehicleControlsQuestionCountByCode[code] }
                }
            });
        }

        await context.SaveChangesAsync();
        return test.Id;
    }

    /// <summary>
    /// Defaults to Practice mode - existing (pre-Story-3.6) tests exercise grading only and must
    /// never be affected by the new Test-mode-only time-limit check.
    /// </summary>
    private async Task<(int AttemptId, Guid LearnerProfileId)> StartAttemptAsync(
        int testId, AttemptMode mode = AttemptMode.Practice)
    {
        var startHandler = new StartAttemptCommandHandler(CreateFactory(), _mapper);
        var learnerProfileId = Guid.NewGuid();
        var startResult = await startHandler.Handle(
            new StartAttemptCommand { LearnerProfileId = learnerProfileId, TestId = testId, Mode = mode },
            CancellationToken.None);
        Assert.True(startResult.Succeeded);
        return (startResult.Data!.Id, learnerProfileId);
    }

    /// <summary>
    /// Re-reads a started Attempt's persisted AttemptQuestions/AttemptAnswerOptions from a FRESH
    /// context - used to look up each question's correct/wrong option id so a test can construct
    /// an exact set of right/wrong SubmitAttemptAnswer entries.
    /// </summary>
    private async Task<Attempt> ReadAttemptAsync(int attemptId)
    {
        await using var context = new ApplicationDbContext(_options);
        return await context.Attempts
            .Include(a => a.AttemptQuestions.OrderBy(q => q.DisplayOrder))
            .ThenInclude(q => q.AttemptAnswerOptions)
            .SingleAsync(a => a.Id == attemptId);
    }

    private static SubmitAttemptAnswer CorrectAnswerFor(AttemptQuestion question) =>
        new()
        {
            AttemptQuestionId = question.Id,
            SelectedAttemptAnswerOptionId = question.AttemptAnswerOptions.Single(o => o.IsCorrect).Id
        };

    private static SubmitAttemptAnswer WrongAnswerFor(AttemptQuestion question) =>
        new()
        {
            AttemptQuestionId = question.Id,
            SelectedAttemptAnswerOptionId = question.AttemptAnswerOptions.Single(o => !o.IsCorrect).Id
        };

    [Fact]
    public async Task Submit_SingleCodeAllSectionsPass_ReturnsOverallPassedWithEverySectionPassed()
    {
        // Arrange: 1 question per section, PassMark 1 - answering every question correctly passes
        // every section and therefore the whole (single) code and the overall result.
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId);
        var attempt = await ReadAttemptAsync(attemptId);

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new SubmitAttemptCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                Answers = attempt.AttemptQuestions.Select(CorrectAnswerFor).ToList()
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.Equal(attemptId, dto.AttemptId);
        Assert.True(dto.Passed);
        var codeResult = Assert.Single(dto.CodeResults);
        Assert.Equal(LicenceCode.Code1, codeResult.Code);
        Assert.True(codeResult.Passed);
        Assert.Equal(3, codeResult.SectionResults.Count);
        Assert.All(codeResult.SectionResults, sr => Assert.True(sr.Passed));
        Assert.All(codeResult.SectionResults, sr => Assert.Equal(1, sr.CorrectCount));

        // DB round-trip: SubmittedAt set, CodeResult/SectionResult persisted, selections recorded.
        await using var verifyContext = new ApplicationDbContext(_options);
        var savedAttempt = await verifyContext.Attempts.SingleAsync(a => a.Id == attemptId);
        Assert.NotNull(savedAttempt.SubmittedAt);

        var savedCodeResults = await verifyContext.CodeResults
            .Include(cr => cr.SectionResults)
            .Where(cr => cr.AttemptId == attemptId)
            .ToListAsync();
        var savedCodeResult = Assert.Single(savedCodeResults);
        Assert.True(savedCodeResult.Passed);
        Assert.Equal(3, savedCodeResult.SectionResults.Count);

        var attemptQuestionIds = await verifyContext.AttemptQuestions
            .Where(q => q.AttemptId == attemptId)
            .Select(q => q.Id)
            .ToListAsync();
        var selectedCount = await verifyContext.AttemptAnswerOptions
            .CountAsync(o => o.IsSelected && attemptQuestionIds.Contains(o.AttemptQuestionId));
        Assert.Equal(3, selectedCount);
    }

    [Fact]
    public async Task Submit_SingleCodeOneSectionFails_ThatSectionFailsOthersPassOverallFails()
    {
        // Arrange: 2 questions per section, PassMark 2 (must get both right to pass a section).
        // VehicleControls is deliberately answered only 1-of-2 correct so IT fails while Rules and
        // Signs both pass fully.
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 2,
            [SectionType.Signs] = 2,
            [SectionType.VehicleControls] = 2
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 2, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId);
        var attempt = await ReadAttemptAsync(attemptId);

        var answers = new List<SubmitAttemptAnswer>();
        foreach (var question in attempt.AttemptQuestions)
        {
            if (question.Section == SectionType.VehicleControls)
            {
                // Answer only the first VehicleControls question correctly; leave the second wrong.
                var isFirst = attempt.AttemptQuestions
                    .Where(q => q.Section == SectionType.VehicleControls)
                    .OrderBy(q => q.DisplayOrder)
                    .First().Id == question.Id;
                answers.Add(isFirst ? CorrectAnswerFor(question) : WrongAnswerFor(question));
            }
            else
            {
                answers.Add(CorrectAnswerFor(question));
            }
        }

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = answers },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.False(dto.Passed);
        var codeResult = Assert.Single(dto.CodeResults);
        Assert.False(codeResult.Passed);

        var rulesResult = codeResult.SectionResults.Single(sr => sr.Section == SectionType.Rules);
        var signsResult = codeResult.SectionResults.Single(sr => sr.Section == SectionType.Signs);
        var vehicleControlsResult = codeResult.SectionResults.Single(sr => sr.Section == SectionType.VehicleControls);

        Assert.True(rulesResult.Passed);
        Assert.True(signsResult.Passed);
        Assert.False(vehicleControlsResult.Passed);
        Assert.Equal(1, vehicleControlsResult.CorrectCount);
        Assert.Equal(2, vehicleControlsResult.PassMark);
    }

    [Fact]
    public async Task Submit_CombinationPartialPass_Code1PassesCode2FailsOverallFails()
    {
        // Arrange: Code1+Code2 combination. Shared Rules/Signs answered correctly (passes for both
        // codes identically). Code1's VehicleControls answered correctly (passes); Code2's
        // VehicleControls answered wrong (fails) - a genuine partial pass across codes.
        var testId = await SeedPublishedCombinationTestAsync(
            new Dictionary<LicenceCode, int> { [LicenceCode.Code1] = 1, [LicenceCode.Code2] = 1 });
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId);
        var attempt = await ReadAttemptAsync(attemptId);

        var answers = new List<SubmitAttemptAnswer>();
        foreach (var question in attempt.AttemptQuestions)
        {
            if (question.Section == SectionType.VehicleControls && question.Code == LicenceCode.Code2)
                answers.Add(WrongAnswerFor(question));
            else
                answers.Add(CorrectAnswerFor(question));
        }

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = answers },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.False(dto.Passed);
        Assert.Equal(2, dto.CodeResults.Count);

        var code1Result = dto.CodeResults.Single(cr => cr.Code == LicenceCode.Code1);
        var code2Result = dto.CodeResults.Single(cr => cr.Code == LicenceCode.Code2);

        Assert.True(code1Result.Passed);
        Assert.False(code2Result.Passed);

        // Both codes see the SAME shared Rules/Signs result (independently graded per code, but
        // identical since the underlying questions/answers are shared).
        Assert.All(code1Result.SectionResults.Where(sr => sr.Section != SectionType.VehicleControls), sr => Assert.True(sr.Passed));
        Assert.All(code2Result.SectionResults.Where(sr => sr.Section != SectionType.VehicleControls), sr => Assert.True(sr.Passed));

        Assert.True(code1Result.SectionResults.Single(sr => sr.Section == SectionType.VehicleControls).Passed);
        Assert.False(code2Result.SectionResults.Single(sr => sr.Section == SectionType.VehicleControls).Passed);
    }

    [Fact]
    public async Task Submit_CombinationBothCodesPass_ReturnsOverallPassedWithBothCodeResultsPassed()
    {
        // Arrange: Code1+Code2 combination, all answers correct for BOTH constituent codes. The
        // only other multi-CodeResult test (Submit_CombinationPartialPass_...) has exactly one code
        // passing and one failing, which can't distinguish Passed = codeResults.All(cr => cr.Passed)
        // from a buggy Any/First-based aggregation - this test closes that gap.
        var testId = await SeedPublishedCombinationTestAsync(
            new Dictionary<LicenceCode, int> { [LicenceCode.Code1] = 1, [LicenceCode.Code2] = 1 });
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId);
        var attempt = await ReadAttemptAsync(attemptId);

        var answers = attempt.AttemptQuestions.Select(CorrectAnswerFor).ToList();

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = answers },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.True(dto.Passed);
        Assert.Equal(2, dto.CodeResults.Count);

        var code1Result = dto.CodeResults.Single(cr => cr.Code == LicenceCode.Code1);
        var code2Result = dto.CodeResults.Single(cr => cr.Code == LicenceCode.Code2);

        Assert.True(code1Result.Passed);
        Assert.True(code2Result.Passed);
        Assert.All(code1Result.SectionResults, sr => Assert.True(sr.Passed));
        Assert.All(code2Result.SectionResults, sr => Assert.True(sr.Passed));
    }

    [Fact]
    public async Task Submit_AttemptNotFound_RejectedWithNotFoundException()
    {
        // Arrange
        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SubmitAttemptCommand { AttemptId = int.MaxValue, LearnerProfileId = Guid.NewGuid(), Answers = new List<SubmitAttemptAnswer>() },
            CancellationToken.None));
    }

    [Fact]
    public async Task Submit_WrongLearner_RejectedWithNotFoundException_SameAsResumeBehavior()
    {
        // Arrange
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, _) = await StartAttemptAsync(testId);

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act & Assert: same ownership check as GetAttemptQuery - a mismatched LearnerProfileId is
        // rejected identically to a nonexistent AttemptId (NotFoundException), never leaking the
        // attempt's existence.
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = Guid.NewGuid(), Answers = new List<SubmitAttemptAnswer>() },
            CancellationToken.None));

        // Nothing was graded or marked submitted by the rejected call.
        await using var verifyContext = new ApplicationDbContext(_options);
        var savedAttempt = await verifyContext.Attempts.SingleAsync(a => a.Id == attemptId);
        Assert.Null(savedAttempt.SubmittedAt);
        Assert.Equal(0, await verifyContext.CodeResults.CountAsync(cr => cr.AttemptId == attemptId));
    }

    [Fact]
    public async Task Submit_AlreadySubmitted_RejectedWithNoReGrading()
    {
        // Arrange: submit once successfully.
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId);
        var attempt = await ReadAttemptAsync(attemptId);

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);
        var firstAnswers = attempt.AttemptQuestions.Select(CorrectAnswerFor).ToList();
        var first = await handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = firstAnswers },
            CancellationToken.None);
        Assert.True(first.Succeeded);

        // Act: submit again - this time with entirely wrong answers, to prove the second call
        // isn't silently re-grading with different input.
        var wrongAnswers = attempt.AttemptQuestions.Select(WrongAnswerFor).ToList();
        var second = await handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = wrongAnswers },
            CancellationToken.None);

        // Assert
        Assert.False(second.Succeeded);
        Assert.Contains("already", second.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Exactly one CodeResult set persists (from the first, successful submit) - no re-grading,
        // no duplicate CodeResult rows.
        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(1, await verifyContext.CodeResults.CountAsync(cr => cr.AttemptId == attemptId));
        var codeResult = await verifyContext.CodeResults.SingleAsync(cr => cr.AttemptId == attemptId);
        Assert.True(codeResult.Passed);
    }

    [Fact]
    public async Task Submit_UnansweredQuestion_GradedAsIncorrectNoCrash()
    {
        // Arrange: 1 question per section, PassMark 1. VehicleControls' single question is left
        // entirely absent from the submitted Answers list (not even a wrong selection).
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId);
        var attempt = await ReadAttemptAsync(attemptId);

        var answers = attempt.AttemptQuestions
            .Where(q => q.Section != SectionType.VehicleControls)
            .Select(CorrectAnswerFor)
            .ToList();

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = answers },
            CancellationToken.None);

        // Assert: no crash, the unanswered section is simply graded 0-correct and fails.
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.False(dto.Passed);
        var vehicleControlsResult = dto.CodeResults.Single().SectionResults.Single(sr => sr.Section == SectionType.VehicleControls);
        Assert.Equal(0, vehicleControlsResult.CorrectCount);
        Assert.False(vehicleControlsResult.Passed);
    }

    [Fact]
    public async Task Submit_AnswerReferencingForeignQuestionOrOption_IsIgnoredNotAnError()
    {
        // Arrange: a real, valid AttemptQuestion from a SECOND, unrelated attempt is included in
        // the submitted Answers list alongside legitimate answers for the real attempt. Boundaries
        // require this to be silently ignored, not an error and not a crash. Two questions per
        // section (PassMark 1) so the one question whose entry is swapped for a foreign-option
        // reference below can still have its section pass via its sibling question - a real
        // AttemptQuestionId submitted twice (once correctly, once with the foreign-option
        // reference) would itself now be rejected as a duplicate answer (Story 3.5 integrity fix),
        // so the foreign-option entry REPLACES that question's entry rather than sitting alongside it.
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 2, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId);
        var (otherAttemptId, _) = await StartAttemptAsync(testId);
        var attempt = await ReadAttemptAsync(attemptId);
        var otherAttempt = await ReadAttemptAsync(otherAttemptId);

        // thisQuestion's own correct entry is deliberately excluded below and replaced with the
        // foreign-option entry instead of being duplicated alongside it.
        var thisQuestion = attempt.AttemptQuestions.First();
        var answers = attempt.AttemptQuestions
            .Where(q => q.Id != thisQuestion.Id)
            .Select(CorrectAnswerFor)
            .ToList();

        // Foreign reference: belongs to otherAttempt, not attempt.
        answers.Add(CorrectAnswerFor(otherAttempt.AttemptQuestions.First()));
        // Foreign option reference: a real AttemptQuestionId from THIS attempt, but an option id
        // that belongs to a DIFFERENT question entirely.
        var foreignOptionId = attempt.AttemptQuestions.Last().AttemptAnswerOptions.First().Id;
        answers.Add(new SubmitAttemptAnswer { AttemptQuestionId = thisQuestion.Id, SelectedAttemptAnswerOptionId = foreignOptionId });

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = answers },
            CancellationToken.None);

        // Assert: no crash, and the legitimate correct answers - one per section still correctly
        // answered even with thisQuestion's foreign-option entry ignored - still grade this attempt
        // as passed (PassMark 1 of 2 per section).
        Assert.True(result.Succeeded);
        Assert.True(result.Data!.Passed);

        // The other attempt was never touched by this submission.
        await using var verifyContext = new ApplicationDbContext(_options);
        var otherSaved = await verifyContext.Attempts.SingleAsync(a => a.Id == otherAttemptId);
        Assert.Null(otherSaved.SubmittedAt);
    }

    /// <summary>
    /// Closes the grading-integrity exploit fixed in SubmitAttemptCommandHandler step (3): before
    /// the fix, submitting both a correct AND an incorrect option for the SAME AttemptQuestionId
    /// set both AttemptAnswerOptions' IsSelected to true, and grading's
    /// Any(o =&gt; o.IsSelected &amp;&amp; o.IsCorrect) check then counted the question as correct
    /// regardless of intent - guaranteeing that question (and therefore its section) always passed.
    /// Asserts the rejection happens BEFORE any mutation at all (not merely before the final save):
    /// no SubmittedAt, no CodeResult rows, and no AttemptAnswerOption.IsSelected flipped anywhere on
    /// this attempt, including for the legitimate, non-duplicated answers submitted alongside the
    /// duplicate.
    /// </summary>
    [Fact]
    public async Task Submit_DuplicateAnswerForSameQuestion_RejectedBeforeAnyMutation()
    {
        // Arrange: 1 question per section, PassMark 1. VehicleControls' single question receives
        // TWO entries in Answers - one pointing at its correct option, one at its incorrect option.
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId);
        var attempt = await ReadAttemptAsync(attemptId);

        var duplicatedQuestion = attempt.AttemptQuestions.Single(q => q.Section == SectionType.VehicleControls);
        var answers = attempt.AttemptQuestions
            .Where(q => q.Id != duplicatedQuestion.Id)
            .Select(CorrectAnswerFor)
            .ToList();
        answers.Add(CorrectAnswerFor(duplicatedQuestion));
        answers.Add(WrongAnswerFor(duplicatedQuestion));

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = answers },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("duplicate", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Nothing was persisted or mutated: not SubmittedAt, not a CodeResult, and - crucially - not
        // even the IsSelected flags for the OTHER, non-duplicated correct answers submitted
        // alongside the duplicate, proving the check runs before step (4)'s mutation loop, not just
        // before step (7)'s save.
        await using var verifyContext = new ApplicationDbContext(_options);
        var savedAttempt = await verifyContext.Attempts.SingleAsync(a => a.Id == attemptId);
        Assert.Null(savedAttempt.SubmittedAt);
        Assert.Equal(0, await verifyContext.CodeResults.CountAsync(cr => cr.AttemptId == attemptId));

        var attemptQuestionIds = await verifyContext.AttemptQuestions
            .Where(q => q.AttemptId == attemptId)
            .Select(q => q.Id)
            .ToListAsync();
        var selectedCount = await verifyContext.AttemptAnswerOptions
            .CountAsync(o => o.IsSelected && attemptQuestionIds.Contains(o.AttemptQuestionId));
        Assert.Equal(0, selectedCount);
    }

    /// <summary>
    /// Regression test for the cross-command grading corruption fix in SubmitAttemptCommandHandler
    /// step (4): before the fix, that step only set the newly-submitted option's IsSelected = true
    /// without first clearing sibling options (unlike CheckAnswerCommand, which does clear-then-set).
    /// A Practice-mode learner who calls CheckAnswer with a WRONG option and later Submits a
    /// DIFFERENT (correct) final answer for that SAME question would end up with BOTH options
    /// marked IsSelected = true, and grading's Any(o =&gt; o.IsSelected &amp;&amp; o.IsCorrect) would count
    /// the question correct regardless of the learner's actual final answer. This proves the fix:
    /// after Submit, only the final submitted (correct) option is selected, the previously-checked
    /// wrong option is cleared back to false, and grading reflects the final answer.
    /// </summary>
    [Fact]
    public async Task Submit_AfterPriorCheckAnswerWithDifferentOption_GradesOnlyFinalSubmittedAnswer()
    {
        // Arrange: 1 question per section, PassMark 1, Practice mode (CheckAnswer is Practice-only).
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Practice);
        var attempt = await ReadAttemptAsync(attemptId);

        var targetQuestion = attempt.AttemptQuestions.Single(q => q.Section == SectionType.Rules);
        var wrongOptionId = targetQuestion.AttemptAnswerOptions.Single(o => !o.IsCorrect).Id;
        var correctOptionId = targetQuestion.AttemptAnswerOptions.Single(o => o.IsCorrect).Id;

        // Act (1): CheckAnswer selects the WRONG option for the target question - persists
        // wrongOption.IsSelected = true (Practice mode's retry-friendly clear-then-set behavior).
        var checkAnswerHandler = new CheckAnswerCommandHandler(CreateFactory());
        var checkResult = await checkAnswerHandler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                AttemptQuestionId = targetQuestion.Id,
                SelectedAttemptAnswerOptionId = wrongOptionId
            },
            CancellationToken.None);
        Assert.True(checkResult.Succeeded);
        Assert.False(checkResult.Data!.IsCorrect);

        await using (var midContext = new ApplicationDbContext(_options))
        {
            var wrongOptionAfterCheck = await midContext.AttemptAnswerOptions.SingleAsync(o => o.Id == wrongOptionId);
            Assert.True(wrongOptionAfterCheck.IsSelected);
        }

        // Act (2): Submit the FINAL attempt with a DIFFERENT (correct) answer for the same
        // question - the other two sections answered correctly too, so a pre-fix bug (both options
        // marked selected) would still show up as "all sections pass", masking the corruption;
        // the DB-level assertions below are what actually catches it.
        var answers = attempt.AttemptQuestions
            .Where(q => q.Id != targetQuestion.Id)
            .Select(CorrectAnswerFor)
            .ToList();
        answers.Add(new SubmitAttemptAnswer { AttemptQuestionId = targetQuestion.Id, SelectedAttemptAnswerOptionId = correctOptionId });

        var submitHandler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);
        var submitResult = await submitHandler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = answers },
            CancellationToken.None);

        // Assert: grading reflects ONLY the final submitted (correct) answer.
        Assert.True(submitResult.Succeeded);
        Assert.True(submitResult.Data!.Passed);
        var rulesResult = submitResult.Data!.CodeResults.Single().SectionResults.Single(sr => sr.Section == SectionType.Rules);
        Assert.True(rulesResult.Passed);
        Assert.Equal(1, rulesResult.CorrectCount);

        // A fresh DB read confirms the previously-checked wrong option is now cleared back to
        // false - not left stuck true alongside the newly-selected correct option.
        await using var verifyContext = new ApplicationDbContext(_options);
        var wrongOptionAfterSubmit = await verifyContext.AttemptAnswerOptions.SingleAsync(o => o.Id == wrongOptionId);
        var correctOptionAfterSubmit = await verifyContext.AttemptAnswerOptions.SingleAsync(o => o.Id == correctOptionId);
        Assert.False(wrongOptionAfterSubmit.IsSelected);
        Assert.True(correctOptionAfterSubmit.IsSelected);
    }

    /// <summary>
    /// Backdates an already-started Attempt's StartedAt directly in the database, so a test can
    /// deterministically simulate elapsed time without actually waiting - used only by the
    /// Story 3.6 timing matrix tests below.
    /// </summary>
    private async Task BackdateAttemptStartedAtAsync(int attemptId, TimeSpan howLongAgo)
    {
        await using var context = new ApplicationDbContext(_options);
        var attempt = await context.Attempts.SingleAsync(a => a.Id == attemptId);
        attempt.StartedAt = DateTime.UtcNow - howLongAgo;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Submit_TestModeWithinTimeLimit_GradesNormally()
    {
        // Arrange: TestConfig.TimeLimitMinutes defaults to 60 (SeedPublishedTestAsync) - backdate
        // StartedAt by only 30 minutes, comfortably within the limit.
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Test);
        await BackdateAttemptStartedAtAsync(attemptId, TimeSpan.FromMinutes(30));
        var attempt = await ReadAttemptAsync(attemptId);

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);
        var clientSubmittedAt = DateTime.UtcNow;

        // Act
        var result = await handler.Handle(
            new SubmitAttemptCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                Answers = attempt.AttemptQuestions.Select(CorrectAnswerFor).ToList(),
                ClientSubmittedAt = clientSubmittedAt
            },
            CancellationToken.None);

        // Assert: grades normally, exactly as Story 3.5.
        Assert.True(result.Succeeded);
        Assert.True(result.Data!.Passed);

        await using var verifyContext = new ApplicationDbContext(_options);
        var savedAttempt = await verifyContext.Attempts.SingleAsync(a => a.Id == attemptId);
        Assert.NotNull(savedAttempt.SubmittedAt);
        // ClientSubmittedAt is stored verbatim for diagnostics only.
        Assert.Equal(clientSubmittedAt, savedAttempt.ClientSubmittedAt);
    }

    [Fact]
    public async Task Submit_TestModeLate_RejectedNothingPersisted()
    {
        // Arrange: TestConfig.TimeLimitMinutes defaults to 60 - backdate StartedAt by 90 minutes,
        // well past the limit.
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Test);
        await BackdateAttemptStartedAtAsync(attemptId, TimeSpan.FromMinutes(90));
        var attempt = await ReadAttemptAsync(attemptId);

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new SubmitAttemptCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                Answers = attempt.AttemptQuestions.Select(CorrectAnswerFor).ToList()
            },
            CancellationToken.None);

        // Assert: rejected with a clear message, nothing persisted - not SubmittedAt, not a
        // CodeResult, and no selections recorded.
        Assert.False(result.Succeeded);
        Assert.Contains("time limit", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = new ApplicationDbContext(_options);
        var savedAttempt = await verifyContext.Attempts.SingleAsync(a => a.Id == attemptId);
        Assert.Null(savedAttempt.SubmittedAt);
        Assert.Equal(0, await verifyContext.CodeResults.CountAsync(cr => cr.AttemptId == attemptId));

        var attemptQuestionIds = await verifyContext.AttemptQuestions
            .Where(q => q.AttemptId == attemptId)
            .Select(q => q.Id)
            .ToListAsync();
        var selectedCount = await verifyContext.AttemptAnswerOptions
            .CountAsync(o => o.IsSelected && attemptQuestionIds.Contains(o.AttemptQuestionId));
        Assert.Equal(0, selectedCount);
    }

    [Fact]
    public async Task Submit_PracticeModeArbitrarilyLate_GradesNormallyNoDeadline()
    {
        // Arrange: Practice mode NEVER enforces the time limit, regardless of TimeLimitMinutes -
        // backdate StartedAt by a full day, far beyond the 60-minute configured limit.
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Practice);
        await BackdateAttemptStartedAtAsync(attemptId, TimeSpan.FromDays(1));
        var attempt = await ReadAttemptAsync(attemptId);

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new SubmitAttemptCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                Answers = attempt.AttemptQuestions.Select(CorrectAnswerFor).ToList()
            },
            CancellationToken.None);

        // Assert: grades normally - no deadline in Practice mode.
        Assert.True(result.Succeeded);
        Assert.True(result.Data!.Passed);

        await using var verifyContext = new ApplicationDbContext(_options);
        var savedAttempt = await verifyContext.Attempts.SingleAsync(a => a.Id == attemptId);
        Assert.NotNull(savedAttempt.SubmittedAt);
    }

    [Fact]
    public async Task Submit_ClientSubmittedAtIsStoredButNeverUsedForLatenessCheck()
    {
        // Arrange: a Test-mode attempt, backdated well past the time limit. The client supplies
        // an early ClientSubmittedAt (as if submitted right at start) - Boundaries require this
        // to be stored for DIAGNOSTICS ONLY and never substituted for the server's own
        // DateTime.UtcNow in the lateness check, so the submission must still be rejected as late.
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Test);
        await BackdateAttemptStartedAtAsync(attemptId, TimeSpan.FromMinutes(90));
        var attempt = await ReadAttemptAsync(attemptId);

        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act: ClientSubmittedAt deceptively claims the submission happened immediately at start.
        var result = await handler.Handle(
            new SubmitAttemptCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                Answers = attempt.AttemptQuestions.Select(CorrectAnswerFor).ToList(),
                ClientSubmittedAt = attempt.StartedAt
            },
            CancellationToken.None);

        // Assert: still rejected as late - the client-supplied timestamp never overrides the
        // server-computed elapsed time.
        Assert.False(result.Succeeded);
        Assert.Contains("time limit", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A DbContext subclass used ONLY by
    /// Submit_ConcurrentDoubleSubmit_UniqueIndexViolationIsCaughtAndReturnsAlreadySubmitted below, to
    /// force the exact interleaving a real concurrent double-submit race produces: this context's
    /// SaveChangesAsync is the handler's own step (7) save, and immediately before it runs, a
    /// SEPARATE ApplicationDbContext instance ("the other concurrent request that already won")
    /// inserts and commits a competing CodeResult row for the same (AttemptId, Code). That makes
    /// this context's own insert violate CodeResultConfiguration's unique index on
    /// (AttemptId, Code) inside its SaveChangesAsync call, which is exactly the DbUpdateException
    /// the handler's try/catch around step (7) exists to convert into a Result.Failure.
    /// </summary>
    private class RaceInjectingApplicationDbContext : ApplicationDbContext
    {
        private readonly DbContextOptions<ApplicationDbContext> _raceOptions;
        private readonly Action _injectRace;
        private bool _injected;

        public RaceInjectingApplicationDbContext(DbContextOptions<ApplicationDbContext> options, Action injectRace)
            : base(options)
        {
            _raceOptions = options;
            _injectRace = injectRace;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_injected)
            {
                _injected = true;
                _injectRace();
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Proves CodeResultConfiguration's unique index on (AttemptId, Code) actually makes
    /// SubmitAttemptCommandHandler's step (7) try/catch reachable and correct: rather than trying to
    /// simulate true multi-threading, this forces the handler's own SaveChangesAsync call to run
    /// AFTER a separate, independent context has already inserted the competing CodeResult row for
    /// the same (AttemptId, Code) - the precise interleaving a real race produces, where the
    /// handler's early "already submitted" check at step (2) still saw SubmittedAt == null (so it
    /// proceeds through grading) but its own commit at step (7) is the one that loses.
    /// </summary>
    [Fact]
    public async Task Submit_ConcurrentDoubleSubmit_UniqueIndexViolationIsCaughtAndReturnsAlreadySubmitted()
    {
        // Arrange
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId);
        var attempt = await ReadAttemptAsync(attemptId);
        var answers = attempt.AttemptQuestions.Select(CorrectAnswerFor).ToList();

        // The "other concurrent request that already won": inserts its own CodeResult for this
        // attempt's (AttemptId, Code1) directly, bypassing the handler entirely, and marks the
        // Attempt submitted - exactly what a genuinely concurrent winning SubmitAttemptCommand call
        // would have persisted by the time this test's handler call reaches ITS OWN
        // SaveChangesAsync.
        void InjectWinningConcurrentSubmit()
        {
            using var otherContext = new ApplicationDbContext(_options);
            otherContext.CodeResults.Add(new CodeResult
            {
                AttemptId = attemptId,
                Code = LicenceCode.Code1,
                Passed = true,
                SectionResults = new List<SectionResult>
                {
                    new() { Section = SectionType.Rules, CorrectCount = 1, PassMark = 1, Passed = true },
                    new() { Section = SectionType.Signs, CorrectCount = 1, PassMark = 1, Passed = true },
                    new() { Section = SectionType.VehicleControls, CorrectCount = 1, PassMark = 1, Passed = true }
                }
            });
            var otherAttempt = otherContext.Attempts.Single(a => a.Id == attemptId);
            otherAttempt.SubmittedAt = DateTime.UtcNow;
            otherContext.SaveChanges();
        }

        var raceFactoryMock = new Mock<IApplicationDbContextFactory>();
        raceFactoryMock
            .Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (IApplicationDbContext)new RaceInjectingApplicationDbContext(
                _options, InjectWinningConcurrentSubmit));

        var handler = new SubmitAttemptCommandHandler(raceFactoryMock.Object, _mapper);

        // Act: this handler call loads the Attempt with SubmittedAt == null (the race injection
        // hasn't happened yet), passes steps (2)/(3)'s checks, grades normally, and only THEN - at
        // its own step (7) SaveChangesAsync - does the injected "other" context's commit happen
        // first, so this call's insert is the one that violates the unique index.
        var result = await handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = answers },
            CancellationToken.None);

        // Assert: the handler's own catch converts the DbUpdateException into the same
        // "already submitted" failure the early-check path (step 2) returns, not an unhandled
        // exception and not a false success.
        Assert.False(result.Succeeded);
        Assert.Contains("already", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Exactly one CodeResult set survives - the "other" concurrent winner's - never a second,
        // duplicate set from this losing call.
        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(1, await verifyContext.CodeResults.CountAsync(cr => cr.AttemptId == attemptId));
        var survivingCodeResult = await verifyContext.CodeResults.SingleAsync(cr => cr.AttemptId == attemptId);
        Assert.True(survivingCodeResult.Passed);
    }

    /// <summary>
    /// Mirrors GetAttemptQueryHandlerTests.cs's
    /// Resume_WrongLearnerMessage_IsIdenticalToNonexistentIdMessage_ForTheSameAttemptId exactly, for
    /// SubmitAttemptCommand's identical ownership check: closes the gap where a future code change
    /// could split the wrong-learner and nonexistent-id cases into differently-worded failures and
    /// leak an attempt's existence to an unauthorized caller.
    /// </summary>
    [Fact]
    public async Task Submit_WrongLearnerMessage_IsIdenticalToNonexistentIdMessage_ForTheSameAttemptId()
    {
        // Arrange: seed a real, persisted Attempt to learn a concrete AttemptId.
        var passMarks = new Dictionary<SectionType, int>
        {
            [SectionType.Rules] = 1,
            [SectionType.Signs] = 1,
            [SectionType.VehicleControls] = 1
        };
        var testId = await SeedPublishedTestAsync(perSectionCount: 1, passMarkBySection: passMarks);
        var (attemptId, _) = await StartAttemptAsync(testId);
        var handler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);

        // Act: wrong learner - the AttemptId exists, but belongs to a different LearnerProfileId.
        var wrongLearnerException = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = Guid.NewGuid(), Answers = new List<SubmitAttemptAnswer>() },
            CancellationToken.None));

        // Act: nonexistent - the exact same AttemptId value, resolved against a second, completely
        // empty database where no Attempt with that id was ever created. Using the same numeric id
        // in both cases isolates the comparison to "why" the attempt can't be resolved, so the
        // resulting messages are directly comparable.
        using var emptyConnection = new SqliteConnection("DataSource=:memory:");
        emptyConnection.Open();
        var emptyOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(emptyConnection).Options;
        using (var schemaContext = new ApplicationDbContext(emptyOptions))
        {
            schemaContext.Database.EnsureCreated();
        }

        var emptyFactoryMock = new Mock<IApplicationDbContextFactory>();
        emptyFactoryMock
            .Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (IApplicationDbContext)new ApplicationDbContext(emptyOptions));
        var emptyHandler = new SubmitAttemptCommandHandler(emptyFactoryMock.Object, _mapper);

        var nonexistentException = await Assert.ThrowsAsync<NotFoundException>(() => emptyHandler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = Guid.NewGuid(), Answers = new List<SubmitAttemptAnswer>() },
            CancellationToken.None));

        // Assert: not just the same exception type, but byte-for-byte the same message. The spec
        // requires the wrong-learner case to be indistinguishable from a genuinely nonexistent id
        // ("identical to Story 3.3's resume behavior") - a future change that gave the wrong-learner
        // case a different message while keeping "not found" for the missing-id case would leak
        // existence information and must fail this test.
        Assert.Equal(nonexistentException.Message, wrongLearnerException.Message);
    }
}
